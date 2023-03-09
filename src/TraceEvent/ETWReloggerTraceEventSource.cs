#define V4_5_Runtime
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Runtime.InteropServices;
using TraceReloggerLib;
using Microsoft.Diagnostics.Utilities;

#pragma warning disable 0414 // This is is because m_scratchBufferSize was #if conditionally removed, and I don't want it to complain about it.  

namespace Microsoft.Diagnostics.Tracing
{
    /// <summary>
    /// ETWReloggerTraceEventSource is designed to be able to write ETW files using an existing ETW input stream (either a file, files or real time session) as a basis. 
    /// The relogger capabilities only exist on Windows 8 OSes and beyond.  
    /// 
    /// The right way to think about this class is that it is just like ETWTraceEventSource, but it also has a output file associated with it, and WriteEvent APIs that
    /// can be used to either copy events from the event stream (the common case), or inject new events (high level stats).  
    /// </summary>
    public unsafe class ETWReloggerTraceEventSource : TraceEventDispatcher
    {
        /// <summary>
        /// Create an ETWReloggerTraceEventSource that can takes its input from the family of etl files inputFileName
        /// and can write them to the ETL file outputFileName (.kernel*.etl, .user*.etl .clr*.etl)
        /// 
        /// This is a shortcut for  ETWReloggerTraceEventSource(inputFileName, TraceEventSourceType.MergeAll, outputFileStream)
        /// </summary>
        public ETWReloggerTraceEventSource(string inputFileName, string outputFileName)
            : this(inputFileName, TraceEventSourceType.MergeAll, outputFileName)
        { }

        /// <summary>
        /// Create an ETWReloggerTraceEventSource that can takes its input from a variety of sources (either a single file,
        /// a set of files, or a real time ETW session (based on 'type'), and can write these events to a new ETW output
        /// file 'outputFileName. 
        /// </summary>
        public ETWReloggerTraceEventSource(string fileOrSessionName, TraceEventSourceType type, string outputFileName)
            : base()
        {
            if (!OperatingSystemVersion.AtLeast(62))
            {
                throw new NotSupportedException("System Tracing is only supported on Windows 8 and above.");
            }

            m_relogger = new CTraceRelogger();
            if (type == TraceEventSourceType.FileOnly)
            {
                m_traceHandleForFirstStream = m_relogger.AddLogfileTraceStream(fileOrSessionName, IntPtr.Zero);
            }
            else if (type == TraceEventSourceType.Session)
            {
                m_traceHandleForFirstStream = m_relogger.AddRealtimeTraceStream(fileOrSessionName, IntPtr.Zero);
            }
            else
            {
                Debug.Assert(type == TraceEventSourceType.MergeAll);
                List<string> logFileNames = ETWTraceEventSource.GetMergeAllLogFiles(fileOrSessionName);
                bool first = true;
                foreach (var logFileName in logFileNames)
                {
                    var handle = m_relogger.AddLogfileTraceStream(logFileName, IntPtr.Zero);
                    if (first)
                    {
                        m_traceHandleForFirstStream = handle;
                        first = false;
                    }
                }
            }

            m_relogger.SetOutputFilename(outputFileName);
            m_myCallbacks = new ReloggerCallbacks(this);
            m_relogger.RegisterCallback(m_myCallbacks);
            m_scratchBufferSize = 0;
            m_scratchBuffer = null;
        }

        /// <summary>
        /// The output file can use a compressed form or not.  Compressed forms can only be read on Win8 and beyond.   Defaults to true.  
        /// </summary>
        public bool OutputUsesCompressedFormat { set { m_relogger.SetCompressionMode((sbyte)(value ? 1 : 0)); } }

        /// <summary>
        /// Writes an event from the input stream to the output stream of events. 
        /// </summary>
        public void WriteEvent(TraceEvent data)
        {
            if (data.eventRecord != m_curTraceEventRecord)
            {
                throw new InvalidOperationException("Currently can only write the event being processed by the callback");
            }

            m_relogger.Inject(m_curITraceEvent);
        }

        /// <summary>
        /// Connect the given EventSource so any events logged from it will go to the output stream of events.   
        /// Once connected, you may only write events from this EventSource while processing the input stream
        /// (that is during the callback of an input stream event), because the context for the EventSource event
        /// (e.g. timestamp, proesssID, threadID ...) will be derived from the current event being processed by
        /// the input stream.  
        /// </summary>
        public void ConnectEventSource(EventSource eventSource)
        {
            if (m_eventListener == null)
            {
                m_eventListener = new ReloggerEventListener(this);
            }

            m_eventListener.EnableEvents(eventSource, EventLevel.Verbose, (EventKeywords)(-1));
        }

#if true // TODO Decide if we want to expose these or not, ConnenctEventSource may be enough.  These are a bit clunky especially but do allow the
        // ability to modify events you don't own, which may be useful.  

        /// <summary>
        /// Writes an event that did not exist previously into the data stream, The context data (time, process, thread, activity, comes from 'an existing event') 
        /// </summary>
        public unsafe void WriteEvent(Guid providerId, ref _EVENT_DESCRIPTOR eventDescriptor, TraceEvent template, params object[] payload)
        {
            if (template.eventRecord != m_curTraceEventRecord)
                throw new InvalidOperationException("Currently can only write the event being processed by the callback");

            // Make a copy of the template so we can modify it
            var newEvent = m_curITraceEvent.Clone();

            fixed (_EVENT_DESCRIPTOR* fixedEventDescr = &eventDescriptor)
            {
                // The interop assembly has its own def of EventDescriptor, but they are identical, so we use unsafe casting to 
                // bridge the gap.  
                _EVENT_DESCRIPTOR* ptrDescr = (_EVENT_DESCRIPTOR*)fixedEventDescr;

                newEvent.SetEventDescriptor(ref *ptrDescr);
                newEvent.SetProviderId(ref providerId);
                SetPayload(newEvent, payload);
                m_relogger.Inject(newEvent);
            }
        }
        /// <summary>
        /// Writes an event that did not exist previously into the data stream, The context data (time, process, thread, activity, comes from 'an existing event') is given explicitly
        /// </summary>
        public unsafe void WriteEvent(Guid providerId, ref _EVENT_DESCRIPTOR eventDescriptor, DateTime timeStamp, int processId, int processorIndex, int threadID, Guid activityID, params object[] payload)
        {

            // Today we always create 64 bit events on 64 bit OSes.  
            var newEvent = m_relogger.CreateEventInstance(m_traceHandleForFirstStream,
                (pointerSize == 8) ? TraceEventNativeMethods.EVENT_HEADER_FLAG_64_BIT_HEADER : TraceEventNativeMethods.EVENT_HEADER_FLAG_32_BIT_HEADER);

            fixed (_EVENT_DESCRIPTOR* fixedEventDescr = &eventDescriptor)
            {
                // The interop assembly has its own def of EventDescriptor, but they are identical, so we use unsafe casting to 
                // bridge the gap.  
                _EVENT_DESCRIPTOR* ptrDescr = (_EVENT_DESCRIPTOR*)fixedEventDescr;

                newEvent.SetEventDescriptor(ref *ptrDescr);
                newEvent.SetProviderId(ref providerId);
                _LARGE_INTEGER fileTimeStamp = new _LARGE_INTEGER();
                fileTimeStamp.QuadPart = timeStamp.ToFileTimeUtc();
                newEvent.SetTimeStamp(ref fileTimeStamp);
                newEvent.SetProcessId((uint)processId);
                newEvent.SetProcessorIndex((uint)processorIndex);
                newEvent.SetThreadId((uint)threadID);
                newEvent.SetActivityId(ref activityID);
                SetPayload(newEvent, payload);
                m_relogger.Inject(newEvent);
            }
        }
#endif

        #region private
        /// <summary>
        /// implementing TraceEventDispatcher
        /// </summary>
        public override int EventsLost { get { return m_eventsLost; } }
        /// <summary>
        /// implementing TraceEventDispatcher
        /// </summary>
        public override bool Process()
        {
            m_relogger.ProcessTrace();
            return !stopProcessing;
        }

        /// <summary>
        /// Implements TraceEventDispatcher.Dispose
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (m_relogger != null)
                {
                    Marshal.FinalReleaseComObject(m_relogger);      // Force the com object to die.  
                }

                if (m_eventListener != null)
                {
                    m_eventListener.Dispose();
                    m_eventListener = null;
                }
                GC.SuppressFinalize(this);
            }

            if (m_scratchBuffer != null)
            {
                Marshal.FreeHGlobal((IntPtr)m_scratchBuffer);
            }

            m_scratchBuffer = null;
            m_scratchBufferSize = 0;
            m_relogger = null;

            m_traceLoggingEventId.Dispose();
        }
        /// <summary>
        /// Implements TraceEventDispatcher.StopProcessing
        /// </summary>
        public override void StopProcessing()
        {
            base.StopProcessing();
            m_relogger.Cancel();
        }

        private unsafe void SetPayload(ITraceEvent newEvent, IList<object> payloadArgs)
        {
            // Where we are writing the serialized data in m_scratchBuffer
            int curBlobPtr = 0;

            // Need to serialize the objects according to ETW serialization conventions.   
            foreach (var payloadArg in payloadArgs)
            {
                var argType = payloadArg.GetType();
                if (argType == typeof(string))
                {
                    var asString = (string)payloadArg;
                    var bytesNeeded = (asString.Length + 1) * 2;
                    var newCurBlobPtr = curBlobPtr + bytesNeeded;
                    EnsureSratchBufferSpace(newCurBlobPtr);

                    // Copy the string. 
                    char* toPtr = (char*)(&m_scratchBuffer[curBlobPtr]);
                    fixed (char* fromPtr = asString)
                    {
                        for (int i = 0; i < asString.Length; i++)
                        {
                            toPtr[i] = fromPtr[i];
                        }

                        toPtr[asString.Length] = '\0';
                    }
                    curBlobPtr = newCurBlobPtr;
                }
                else if (argType == typeof(int))
                {
                    EnsureSratchBufferSpace(curBlobPtr + 4);
                    *((int*)&m_scratchBuffer[curBlobPtr]) = (int)payloadArg;
                    curBlobPtr += 4;
                }
                else if (argType == typeof(long))
                {
                    EnsureSratchBufferSpace(curBlobPtr + 8);
                    *((long*)&m_scratchBuffer[curBlobPtr]) = (long)payloadArg;
                    curBlobPtr += 8;
                }
                else if (argType == typeof(double))
                {
                    EnsureSratchBufferSpace(curBlobPtr + 8);
                    *((double*)&m_scratchBuffer[curBlobPtr]) = (double)payloadArg;
                    curBlobPtr += 8;
                }
                else if (argType == typeof(UInt64))
                {
                    EnsureSratchBufferSpace(curBlobPtr + 8);
                    *((UInt64*)&m_scratchBuffer[curBlobPtr]) = (UInt64)payloadArg;
                    curBlobPtr += 8;
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            newEvent.SetPayload(ref *m_scratchBuffer, (uint)curBlobPtr);
        }

        private void EnsureSratchBufferSpace(int requriedSize)
        {
            if (m_scratchBufferSize < requriedSize)
            {
                if (m_scratchBuffer != null)
                    m_scratchBuffer = (byte*)Marshal.ReAllocHGlobal((IntPtr)m_scratchBuffer, (IntPtr)requriedSize);
                else
                    m_scratchBuffer = (byte*)Marshal.AllocHGlobal(requriedSize);
                m_scratchBufferSize = requriedSize;
            }
        }

        /// <summary>
        /// This is used by the ConnectEventSource to route events from the EventSource to the relogger. 
        /// </summary>
        private class ReloggerEventListener : EventListener
        {
            public ReloggerEventListener(ETWReloggerTraceEventSource relogger)
            {
                m_relogger = relogger;
                m_sentManifest = new bool[4];       // We guess we don't need many EventSOurces (will grow as needed)
            }
            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                SendManifestIfNecessary(eventData.EventSource);

                var relogger = m_relogger;
                var newEvent = relogger.m_relogger.CreateEventInstance(relogger.m_traceHandleForFirstStream, 0);

                // The interop assembly has its own def of EventDescriptor, but they are identical, so we use unsafe casting to 
                // bridge the gap.  
                _EVENT_DESCRIPTOR descr = new _EVENT_DESCRIPTOR();
                descr.Id = (ushort)eventData.EventId;
                descr.Keyword = (ulong)eventData.Keywords;
                descr.Level = (byte)eventData.Level;
                descr.Opcode = (byte)eventData.Opcode;
                descr.Task = (ushort)eventData.Task;
                descr.Version = eventData.Version;
                newEvent.SetEventDescriptor(ref descr);

                // Set the provider to the EventSource
                Guid providerGuid = eventData.EventSource.Guid;
                newEvent.SetProviderId(ref providerGuid);

                // Clone the process ID, thread ID and TimeStamp
                _EVENT_RECORD* eventRecord = (_EVENT_RECORD*)relogger.m_curITraceEvent.GetEventRecord();
                newEvent.SetThreadId(eventRecord->EventHeader.ThreadId);
                newEvent.SetProcessId(eventRecord->EventHeader.ProcessId);
                newEvent.SetTimeStamp(ref eventRecord->EventHeader.TimeStamp);

                // Copy over the payload. 
                relogger.SetPayload(newEvent, eventData.Payload);
                relogger.m_relogger.Inject(newEvent);
            }

            private void SendManifestIfNecessary(EventSource eventSource)
            {
                var eventSourceIdx = EventSourceIndex(eventSource);
                if (m_sentManifest.Length <= eventSourceIdx)
                {
                    var newSentManifest = new bool[eventSourceIdx + 4];
                    Array.Copy(m_sentManifest, newSentManifest, m_sentManifest.Length);
                    m_sentManifest = newSentManifest;
                }

                if (m_sentManifest[eventSourceIdx])
                {
                    return;
                }

                m_sentManifest[eventSourceIdx] = true;

                // Get the manifest and send it.  
                var manifestStr = EventSource.GenerateManifest(eventSource.GetType(), eventSource.Name);
                var manifestBytes = System.Text.Encoding.UTF8.GetBytes(manifestStr);
                m_relogger.SendManifest(manifestBytes, eventSource);
            }

            private ETWReloggerTraceEventSource m_relogger;
            private bool[] m_sentManifest;                  // indexed by EventSource identity (index)
        }

        /// <summary>
        /// This is the class the Win32 APIs call back on.  
        /// </summary>
        private unsafe class ReloggerCallbacks : ITraceEventCallback
        {
            public ReloggerCallbacks(ETWReloggerTraceEventSource source) { m_source = source; }

            public void OnBeginProcessTrace(ITraceEvent headerEvent, CTraceRelogger relogger)
            {
                var rawData = (TraceEventNativeMethods.EVENT_RECORD*)headerEvent.GetEventRecord();
                Initialize(rawData);
            }

            public void OnEvent(ITraceEvent eventData, CTraceRelogger relogger)
            {
                var rawData = (TraceEventNativeMethods.EVENT_RECORD*)eventData.GetEventRecord();
                var source = m_source;

                if (source.stopProcessing)
                {
                    return;
                }

                // is this the very first event? if so this could be the header event (for real time ETW)
                if (m_source._syncTimeQPC == 0)
                {
                    Initialize(rawData);
                }

                Debug.Assert(rawData->EventHeader.HeaderType == 0);     // if non-zero probably old-style ETW header

                // Give it an event ID if it does not have one.  
                source.m_traceLoggingEventId.TestForTraceLoggingEventAndFixupIfNeeded(rawData);

                // Lookup the event;
                TraceEvent anEvent = source.Lookup(rawData);

                source.m_curITraceEvent = eventData;
                source.m_curTraceEventRecord = anEvent.eventRecord;

                // Keep in mind that for UnhandledTraceEvent 'PrepForCallback' has NOT been called, which means the
                // opcode, guid and eventIds are not correct at this point.  The ToString() routine WILL call
                // this so if that is in your debug window, it will have this side effect (which is good and bad)
                // Looking at rawData will give you the truth however. 
                anEvent.DebugValidate();

                if (anEvent.NeedsFixup)
                {
                    anEvent.FixupData();
                }

                source.Dispatch(anEvent);

                // Release the COM object aggressively  Otherwise you build up quite a few of these before 
                // the GC kicks in and cleans them all up.  
                Marshal.FinalReleaseComObject(source.m_curITraceEvent);
                source.m_curITraceEvent = null;
            }

            public void OnFinalizeProcessTrace(CTraceRelogger relogger)
            {
            }

            private unsafe void Initialize(TraceEventNativeMethods.EVENT_RECORD* rawData)
            {
                var eventHeader = new EventTraceHeaderTraceData(null, 0xFFFF, 0, "EventTrace", KernelTraceEventParser.EventTraceTaskGuid, 0, "Header", KernelTraceEventParser.ProviderGuid, KernelTraceEventParser.ProviderName, null);
                eventHeader.traceEventSource = m_source;
                eventHeader.eventRecord = rawData;
                eventHeader.userData = rawData->UserData;

                if (eventHeader.eventRecord->EventHeader.ProviderId != eventHeader.taskGuid || eventHeader.eventRecord->EventHeader.Opcode != 0)
                {
                    if (m_source._syncTimeQPC == 0)
                    {
                        // We did not get a if (m_source.sessionStartTimeQPC != 0) EventTraceHeaderTraceData either in the OnBeginProcessTrace callback (file based case) or the
                        // first event (real time case).   This is really a problem, as we need that information, but we will go ahead and
                        // try to initialize as best we can. 

                        m_source.pointerSize = ETWTraceEventSource.GetOSPointerSize();
                        m_source.numberOfProcessors = Environment.ProcessorCount;
                        m_source._QPCFreq = Stopwatch.Frequency;
                        m_source._syncTimeUTC = DateTime.UtcNow;
                        m_source._syncTimeQPC = rawData->EventHeader.TimeStamp;

                        m_source.sessionStartTimeQPC = rawData->EventHeader.TimeStamp;
                        m_source.sessionEndTimeQPC = long.MaxValue;
                    }
                    return;
                }

                m_source.m_eventsLost += eventHeader.EventsLost;
                if (m_source._syncTimeQPC != 0)
                {
                    return;
                }

                m_source.pointerSize = eventHeader.PointerSize;
                Debug.Assert(m_source.pointerSize == 4 || m_source.pointerSize == 8);
                m_source.numberOfProcessors = eventHeader.NumberOfProcessors;
                Debug.Assert(m_source.numberOfProcessors < 10000);
                m_source.cpuSpeedMHz = eventHeader.CPUSpeed;
                m_source.utcOffsetMinutes = eventHeader.UTCOffsetMinutes;
                int ver = eventHeader.Version;
                m_source.osVersion = new Version((byte)ver, (byte)(ver >> 8));
                m_source._QPCFreq = eventHeader.PerfFreq;
                m_source._syncTimeUTC = DateTime.FromFileTimeUtc(eventHeader.StartTime100ns);
                m_source._syncTimeQPC = rawData->EventHeader.TimeStamp;
                m_source.sessionStartTimeQPC = rawData->EventHeader.TimeStamp;

                if (eventHeader.EndTime100ns == 0)
                {
                    m_source.sessionEndTimeQPC = long.MaxValue;
                }
                else
                {
                    m_source.sessionEndTimeQPC = m_source.UTCDateTimeToQPC(DateTime.FromFileTimeUtc(eventHeader.EndTime100ns));
                }
            }

            private ETWReloggerTraceEventSource m_source;
        }

        internal unsafe void SendManifest(byte[] rawManifest, EventSource eventSource)
        {
            ManifestEnvelope envelope = new ManifestEnvelope();
            envelope.Format = ManifestEnvelope.ManifestFormats.SimpleXmlFormat;
            envelope.MajorVersion = 1;
            envelope.MinorVersion = 0;
            envelope.Magic = 0x5B;              // An unusual number that can be checked for consistancy. 
            int dataLeft = rawManifest.Length;
            envelope.TotalChunks = (ushort)((dataLeft + (ManifestEnvelope.MaxChunkSize - 1)) / ManifestEnvelope.MaxChunkSize);
            envelope.ChunkNumber = 0;

            if (m_curITraceEvent == null)
            {
                throw new InvalidOperationException("Currently can only write the event being processed by the callback");
            }
            // Make a copy of the template so we can modify it
            var manifestEvent = m_relogger.CreateEventInstance(m_traceHandleForFirstStream, 0);

            var manifestDescr = new _EVENT_DESCRIPTOR() { Id = 0xFFFE, Task = 0xFFFE, Opcode = 0xFE, Keyword = ulong.MaxValue };
            manifestEvent.SetEventDescriptor(ref manifestDescr);

            // Set the provider to the EventSoruce
            Guid providerGuid = eventSource.Guid;
            manifestEvent.SetProviderId(ref providerGuid);

            // Clone the process ID, thread ID and TimeStamp
            _EVENT_RECORD* eventRecord = (_EVENT_RECORD*)m_curITraceEvent.GetEventRecord();
            manifestEvent.SetThreadId(eventRecord->EventHeader.ThreadId);
            manifestEvent.SetProcessId(eventRecord->EventHeader.ProcessId);
            manifestEvent.SetTimeStamp(ref eventRecord->EventHeader.TimeStamp);

            int bufferSize = sizeof(ManifestEnvelope) + Math.Min(ManifestEnvelope.MaxChunkSize, rawManifest.Length);
            byte[] buffer = new byte[bufferSize];
            int manifestIdx = 0;                    // Where we are in the manifest. 
            while (dataLeft > 0)
            {
                // Copy envelope into buffer
                byte* envelopePtr = (byte*)&envelope;
                int bufferIdx = 0;
                while (bufferIdx < sizeof(ManifestEnvelope))
                {
                    buffer[bufferIdx++] = *envelopePtr++;
                }

                // Copy chunk of manifest into buffer 
                while (bufferIdx < buffer.Length && manifestIdx < rawManifest.Length)
                {
                    buffer[bufferIdx++] = rawManifest[manifestIdx++];
                }

                // write the envelope + chunk.  
                fixed (byte* bufferPtr = buffer)
                {
                    manifestEvent.SetPayload(ref *bufferPtr, (uint)bufferIdx);
                    m_relogger.Inject(manifestEvent);
                }
                envelope.ChunkNumber++;
                Debug.Assert(envelope.ChunkNumber <= envelope.TotalChunks);
                dataLeft -= ManifestEnvelope.MaxChunkSize;
            }
            Debug.Assert(envelope.ChunkNumber == envelope.TotalChunks);
        }

        private EventListener m_eventListener;
        private CTraceRelogger m_relogger;
        private ulong m_traceHandleForFirstStream;
        private ReloggerCallbacks m_myCallbacks;
        private int m_eventsLost;
        private byte* m_scratchBuffer;
        private int m_scratchBufferSize;
        private ITraceEvent m_curITraceEvent;                                            // Before we make callbacks we remember the ITraceEvent 
        private TraceEventNativeMethods.EVENT_RECORD* m_curTraceEventRecord;             // This is the TraceEvent eventRecord that corresponds to the ITraceEvent. 
        private TraceLoggingEventId m_traceLoggingEventId;                               // Used to give TraceLogging events Event IDs. 

        #endregion
    }
}
