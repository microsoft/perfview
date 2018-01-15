using FastSerialization;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Tracing.EventPipe
{
    internal unsafe class EventPipeEventSourceV1 : EventPipeEventSource
    {
        public EventPipeEventSourceV1(Deserializer deserializer, string fileName, int version)
            : base(deserializer)
        {
            // V1 EventPipe doesn't have process info. 
            // Since it's from a single process, use the file name as the process name.
            _processName = Path.GetFileNameWithoutExtension(fileName);
            _processId = 0;
            _version = version;

            // V1 EventPipe doesn't have osVersion, cpuSpeedMHz, pointerSize, numberOfProcessors
            osVersion = new Version("0.0.0.0");
            cpuSpeedMHz = 10;
            pointerSize = 8; // V1 EventPipe only supports Linux which is x64 only.
            numberOfProcessors = 1;

            // We need to read the header to get the sync time information here
            ReadHeaderInfo();

            var mem = (TraceEventNativeMethods.EVENT_RECORD*)Marshal.AllocHGlobal(sizeof(TraceEventNativeMethods.EVENT_RECORD));
            *mem = default(TraceEventNativeMethods.EVENT_RECORD);
            _header = mem;

            _eventParser = new EventPipeTraceEventParser(this);
        }

        public override int EventsLost => 0;

        public override bool Process()
        {
            while (_deserializer.Current < _endOfEventStream)
            {
                StreamLabel eventStart = _deserializer.Current;

                // Read the size of the next event.
                uint eventSize = (uint)_deserializer.ReadInt();

                ReadAndDispatchTraceEvent(eventStart, eventSize);
            }

            sessionEndTimeQPC = (long)_lastTimestamp;

            return true;
        }

        internal override string ProcessName(int processID, long timeQPC)
        {
            if (processID == _processId)
            {
                return _processName;
            }

            return base.ProcessName(processID, timeQPC);
        }

        #region Private
        protected virtual void ReadHeaderInfo()
        {
            // Read and check the type name
            _deserializer.ReadStringAndVerify("Microsoft.DotNet.Runtime.EventPipeFile");

            // Read tag and check
            _deserializer.ReadByteAndVerify(0x6 /*EndObject tag*/);

            // Read end of event stream marker
            ForwardReference reference = _deserializer.ReadForwardReference();
            _endOfEventStream = _deserializer.ResolveForwardReference(reference, preserveCurrent: true);

            // Read the date and time of trace start.
            var year = _deserializer.ReadInt16();
            var month = _deserializer.ReadInt16();
            var dayOfWeek = _deserializer.ReadInt16();
            var day = _deserializer.ReadInt16();
            var hour = _deserializer.ReadInt16();
            var minute = _deserializer.ReadInt16();
            var second = _deserializer.ReadInt16();
            var milliseconds = _deserializer.ReadInt16();

            _syncTimeUTC = new DateTime(year, month, day, hour, minute, second, milliseconds, DateTimeKind.Utc);

            // Read the start timestamp.
            sessionStartTimeQPC = (long)_deserializer.ReadInt64();
            _syncTimeQPC = sessionStartTimeQPC;

            // Read the clock frequency.
            _QPCFreq = _deserializer.ReadInt64();
        }

        private void ReadAndDispatchTraceEvent(StreamLabel eventStart, uint eventSize)
        {
            // Read the metadata label.
            StreamLabel metadataLabel = (StreamLabel)_deserializer.ReadInt();

            // 0 is the label for metadata.
            if (metadataLabel == 0)
            {
                ReadAndUpdateEventMetadataDictionary(eventStart);
            }
            else
            {
                EventMetadata eventMetadata;
                if (_eventMetadataDictionary.TryGetValue(metadataLabel, out eventMetadata))
                {
                    ReadAndDispatchTraceEvent(eventMetadata);
                }
                else
                {
                    Debug.Fail($"Unable to find metadata for {metadataLabel}.");

                    // Skip the event
                    _deserializer.Goto(eventStart + eventSize);
                }
            }
        }

        private void ReadAndUpdateEventMetadataDictionary(StreamLabel eventStart)
        {
            var eventMetadata = new EventMetadata();

            // Skip the EventPipeEvent common fields which are not related to metedata
            int commonFieldSize = sizeof(int) // ThreadId
                + sizeof(Int64) // Timestamp
                + sizeof(Guid)  // ActivityId
                + sizeof(Guid); // RelatedActivityId
            _deserializer.Goto(_deserializer.Current + (uint)commonFieldSize);

            // Read the event payload size.
            var payloadSize = _deserializer.ReadInt();
            Debug.Assert(payloadSize >= 0, "Payload size should not be negative.");

            if (_version == 1)
            {
                var providerId = Guid.Empty;
                _deserializer.Read(out providerId);
                eventMetadata.ProviderId = providerId;
            }
            else
            {
                eventMetadata.ProviderName = _deserializer.ReadNullTerminatedUnicodeString();
            }

            eventMetadata.EventId = (uint)_deserializer.ReadInt();
            eventMetadata.Version = (uint)_deserializer.ReadInt();
            var metadataPayloadLength = (uint)_deserializer.ReadInt();

            if (metadataPayloadLength > 0)
            {
                var payloadIdentifierLength = 0;
                if (_version == 1)
                {
                    payloadIdentifierLength = sizeof(Guid);
                }
                else
                {
                    payloadIdentifierLength = (eventMetadata.ProviderName.Length + 1) * sizeof(Char); // +1 for null-terminator
                }
                var actualPayloadSize = payloadIdentifierLength
                    + sizeof(int) // EventId
                    + sizeof(int) // Version
                    + sizeof(int) // MetadataPayloadLength
                    + metadataPayloadLength;
                Debug.Assert(payloadSize == actualPayloadSize, $"The total event payload size is {actualPayloadSize}. But it is expected to be {payloadSize}.");

                // Read EventSource definition: https://github.com/dotnet/coreclr/blob/release/2.0.0/src/mscorlib/shared/System/Diagnostics/Tracing/EventSource.cs#L708
                eventMetadata.EventId = (uint)_deserializer.ReadInt();
                eventMetadata.EventName = _deserializer.ReadNullTerminatedUnicodeString();
                eventMetadata.Keywords = (ulong)_deserializer.ReadInt64();
                eventMetadata.Version = (uint)_deserializer.ReadInt();
                eventMetadata.Level = (uint)_deserializer.ReadInt();

                int parameterCount = _deserializer.ReadInt();
                Debug.Assert(parameterCount >= 0, "Parameter count should not be negative.");

                if (parameterCount > 0)
                {
                    eventMetadata.ParameterDefinitions = new Tuple<TypeCode, string>[parameterCount];
                    for (int i = 0; i < parameterCount; i++)
                    {
                        var type = (uint)_deserializer.ReadInt();
                        var name = _deserializer.ReadNullTerminatedUnicodeString();
                        eventMetadata.ParameterDefinitions[i] = new Tuple<TypeCode, string>((TypeCode)type, name);
                    }
                }

                // add a new template to event parser
                _eventParser.AddTemplate(eventMetadata);
            }

            // Add the new event meta data into dictionary
            _eventMetadataDictionary.Add(eventStart, eventMetadata);

            // Read and verify stack size
            _deserializer.ReadIntAndVerify(0);
        }

        private void ReadAndDispatchTraceEvent(EventMetadata eventMetadata)
        {
            // Read the thread id.
            var threadId = _deserializer.ReadInt();

            // Read the time stamp.
            var timeStamp = _deserializer.ReadInt64();

            // Ensure that timestamps are properly ordered.
            System.Diagnostics.Debug.Assert(_lastTimestamp <= timeStamp);
            _lastTimestamp = timeStamp;

            // Read the activity id.
            Guid activityId;
            _deserializer.Read(out activityId);

            // Read the related activity id.
            Guid relatedActivityId;
            _deserializer.Read(out relatedActivityId);

            // Read the event payload size.
            var payloadSize = _deserializer.ReadInt();
            Debug.Assert(payloadSize >= 0, "Payload size should not be negative.");

            // Allocate memory for the event payload.
            var payload = new byte[payloadSize];
            for (uint i = 0; i < payloadSize; i++)
            {
                // Copy the payload.
                payload[i] = _deserializer.ReadByte();
            }

            // Dispatch the event.
            Dispatch(SynthesizeTraceEvent(eventMetadata, threadId, timeStamp, activityId, relatedActivityId, payload));

            // Read the optional stack.
            var stackSize = _deserializer.ReadInt();
            Debug.Assert(stackSize >= 0, "Stack size should not be negative.");

            // The book keeping event itself will be discarded.
            // The call stack is not interesting.
            if (IsBookKeepingEvent(eventMetadata))
            {
                _deserializer.Goto(_deserializer.Current + (uint)stackSize);
            }
            else
            {
                if (stackSize > 0)
                {
                    // Allocate memory for the stack if it's not empty
                    var stack = new byte[stackSize];
                    // Copy the stack
                    for (uint i = 0; i < stackSize; i++)
                    {
                        stack[i] = _deserializer.ReadByte();
                    }

                    Dispatch(SynthesizeThreadStackWalkEvent(threadId, stack));
                }
            }
        }

        private bool IsBookKeepingEvent(EventMetadata eventMetadata)
        {
            return (eventMetadata.ProviderId == ClrTraceEventParser.ProviderGuid
                    && (eventMetadata.EventId == 139 // MethodDCStartVerboseV2
                    || eventMetadata.EventId == 140 // MethodDCStopVerboseV2
                    || eventMetadata.EventId == 143 // MethodLoadVerbose
                    || eventMetadata.EventId == 144 // MethodUnloadVerbose
                    || eventMetadata.EventId == 190 // MethodILToNativeMap
                    ))
                || (eventMetadata.ProviderId == ClrRundownTraceEventParser.ProviderGuid
                    && (eventMetadata.EventId == 143 // MethodDCStartVerbose
                    || eventMetadata.EventId == 144 // MethodDCStopVerbose
                    || eventMetadata.EventId == 150 // MethodILToNativeMapDCStop
                    ));
        }

        private TraceEvent SynthesizeTraceEvent(EventMetadata eventMetadata, int threadId, long timeStamp, Guid activityId, Guid relatedActivityId, byte[] payload)
        {
            var hdr = InitEventRecord(eventMetadata, threadId, timeStamp, activityId, relatedActivityId, payload);
            TraceEvent traceEvent = Lookup(hdr);
            traceEvent.eventRecord = hdr;
            return traceEvent;
        }

        private TraceEvent SynthesizeThreadStackWalkEvent(int threadId, byte[] stack)
        {
            _header->EventHeader.Size = (ushort)sizeof(TraceEventNativeMethods.EVENT_TRACE_HEADER);
            _header->EventHeader.Flags = 0;
            if (pointerSize == 8)
                _header->EventHeader.Flags |= TraceEventNativeMethods.EVENT_HEADER_FLAG_64_BIT_HEADER;
            else
                _header->EventHeader.Flags |= TraceEventNativeMethods.EVENT_HEADER_FLAG_32_BIT_HEADER;

            _header->EventHeader.ThreadId = threadId;
            _header->EventHeader.ProviderId = SampleProfilerTraceEventParser.ProviderGuid;
            _header->EventHeader.Id = 1; // StackWalk

            _header->ExtendedDataCount = 0;

            _header->UserDataLength = (ushort)stack.Length;
            _header->UserData = GCHandle.Alloc(stack, GCHandleType.Pinned).AddrOfPinnedObject();

            TraceEvent traceEvent = Lookup(_header);
            traceEvent.eventRecord = _header;
            return traceEvent;
        }

        private TraceEventNativeMethods.EVENT_RECORD* InitEventRecord(EventMetadata eventMetadata, int threadId, long timeStamp, Guid activityId, Guid relatedActivityId, byte[] payload)
        {
            _header->EventHeader.Size = (ushort)sizeof(TraceEventNativeMethods.EVENT_TRACE_HEADER);
            _header->EventHeader.Flags = 0;
            if (pointerSize == 8)
                _header->EventHeader.Flags |= TraceEventNativeMethods.EVENT_HEADER_FLAG_64_BIT_HEADER;
            else
                _header->EventHeader.Flags |= TraceEventNativeMethods.EVENT_HEADER_FLAG_32_BIT_HEADER;

            _header->EventHeader.TimeStamp = timeStamp;
            _header->EventHeader.ProviderId = eventMetadata.ProviderId;
            _header->EventHeader.Version = (byte)eventMetadata.Version;
            _header->EventHeader.Level = (byte)eventMetadata.Level;
            _header->EventHeader.Opcode = GetOpcodeFromEventName(eventMetadata.EventName);
            _header->EventHeader.Id = (ushort)eventMetadata.EventId;
            _header->EventHeader.Keyword = eventMetadata.Keywords;

            _header->EventHeader.ActivityId = activityId;
            _header->ExtendedDataCount = 1;
            _header->ExtendedData = (TraceEventNativeMethods.EVENT_HEADER_EXTENDED_DATA_ITEM*)Marshal.AllocHGlobal(sizeof(TraceEventNativeMethods.EVENT_HEADER_EXTENDED_DATA_ITEM));
            _header->ExtendedData->ExtType = TraceEventNativeMethods.EVENT_HEADER_EXT_TYPE_RELATED_ACTIVITYID;
            _header->ExtendedData->DataPtr = (ulong)GCHandle.Alloc(relatedActivityId, GCHandleType.Pinned).AddrOfPinnedObject();

            _header->UserDataLength = (ushort)payload.Length;
            _header->UserData = GCHandle.Alloc(payload, GCHandleType.Pinned).AddrOfPinnedObject();

            _header->BufferContext = new TraceEventNativeMethods.ETW_BUFFER_CONTEXT();
            _header->BufferContext.ProcessorNumber = 0;
            _header->EventHeader.ThreadId = threadId;
            _header->EventHeader.ProcessId = _processId;
            _header->EventHeader.KernelTime = 0;
            _header->EventHeader.UserTime = 0;

            return _header;
        }

        private byte GetOpcodeFromEventName(string name)
        {
            if (name == null)
            {
                return c_UnknownEventOpcode;
            }
            else
            {
                if (name.EndsWith("Start", StringComparison.OrdinalIgnoreCase))
                {
                    return (byte)TraceEventOpcode.Start;
                }
                else if (name.EndsWith("Stop", StringComparison.OrdinalIgnoreCase))
                {
                    return (byte)TraceEventOpcode.Stop;
                }
                else
                {
                    return c_UnknownEventOpcode;
                }
            }
        }

        private Dictionary<StreamLabel, EventMetadata> _eventMetadataDictionary = new Dictionary<StreamLabel, EventMetadata>();
        private TraceEventNativeMethods.EVENT_RECORD* _header;
        private StreamLabel _endOfEventStream;

        private long _lastTimestamp = 0;
        private string _processName;
        private int _processId;
        private int _version;

        private const byte c_UnknownEventOpcode = 0;

        private EventPipeTraceEventParser _eventParser;
        #endregion

    }
}
