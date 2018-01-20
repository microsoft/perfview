using FastSerialization;
using Microsoft.Diagnostics.Tracing.EventPipe;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.DotNet.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Tracing
{
    unsafe public class EventPipeEventSourceNew : TraceEventDispatcher
    {
        public EventPipeEventSourceNew(string fileName)
        {
            _deserializer = new Deserializer(new PinnedStreamReader(fileName, 0x10000), fileName);

            _deserializer.TypeResolver = typeName => System.Type.GetType(typeName);  // resolve types in this assembly (and mscorlib)
            _deserializer.RegisterFactory(typeof(EventPipeFile), delegate { return new EventPipeFile(); });


            _eventPipeFile = (EventPipeFile)_deserializer.GetEntryObject();

            _eventParser = new EventPipeTraceEventParser(this);
        }

        #region private
        // I put these in the private section because they are overrides, and thus don't ADD to the API.  
        public override int EventsLost => 0;
        public override bool Process()
        {
            PinnedStreamReader deserializerReader = (PinnedStreamReader)_deserializer.Reader;

            deserializerReader.Goto(_eventPipeFile._startOfStream);
            while (deserializerReader.Current < _eventPipeFile._endOfEventStream)
            {
                TraceEventNativeMethods.EVENT_RECORD* eventRecord = ReadEvent(deserializerReader);
                if (eventRecord != null)
                {
                    TraceEvent event_ = Lookup(eventRecord);
                    Dispatch(event_);
                    sessionEndTimeQPC = eventRecord->EventHeader.TimeStamp;
                }
            }

            return true;
        }

        private TraceEventNativeMethods.EVENT_RECORD* ReadEvent(PinnedStreamReader reader)
        {
            const int eventSizeGuess = 512;
            EventPipeEventHeader* eventData = (EventPipeEventHeader*)reader.GetPointer(eventSizeGuess);
            if (eventSizeGuess < eventData->EventSize)
                eventData = (EventPipeEventHeader*)reader.GetPointer(eventData->EventSize - sizeof(int));

            Debug.Assert(0 < eventData->EventSize && eventData->EventSize < 0x10000);
            Debug.Assert(0 <= eventData->PayloadSize && eventData->PayloadSize <= eventData->EventSize);
            Debug.Assert(0 <= EventPipeEventHeader.StackBytesSize(eventData) && EventPipeEventHeader.StackBytesSize(eventData) <= eventData->EventSize);
            Debug.Assert(eventData->PayloadSize + EventPipeEventHeader.HeaderSize == eventData->EventSize);

            TraceEventNativeMethods.EVENT_RECORD* ret = null;
            EventPipeEventMetaData metaData;
            if (eventData->MetaDataId == 0)     // Is this a Meta-data event?  
            {
                int payloadSize = eventData->PayloadSize;
                StreamLabel metaDataStreamOffset = reader.Current;
                // Note that this skip invalidates the eventData pointer, so it is important to pull any fields out we need first.  
                reader.Skip(EventPipeEventHeader.HeaderSize);
                metaData = new EventPipeEventMetaData(reader, payloadSize, _eventPipeFile._version);
                _eventMetadataDictionary.Add(metaDataStreamOffset, metaData);
                _eventParser.AddTemplate(metaData);
                int stackBytes = reader.ReadInt32();        // Meta-data events should always have a empty stack.  
                Debug.Assert(stackBytes == 0);
            }
            else
            {
                if (_eventMetadataDictionary.TryGetValue(eventData->MetaDataId, out metaData))
                    ret = metaData.GetEventRecordForEventData(eventData, PointerSize);
                else
                    Debug.Assert(false, "Warning can't find metaData for ID " + eventData->MetaDataId.ToString("x"));

                // Skip the event data, the stack size field and the stack data bytes.  
                reader.Skip(eventData->EventSize + sizeof(int) + EventPipeEventHeader.StackBytesSize(eventData));
            }

            return ret;
        }

        internal override unsafe Guid GetRelatedActivityID(TraceEventNativeMethods.EVENT_RECORD* eventRecord)
        {
            // Recover the EventPipeEventHeader from the payload pointer and then fetch from the header.  
            EventPipeEventHeader* event_ = EventPipeEventHeader.HeaderFromPayloadPointer((byte*)eventRecord->UserData);
            return event_->RelatedActivityID;
        }

        Dictionary<StreamLabel, EventPipeEventMetaData> _eventMetadataDictionary = new Dictionary<StreamLabel, EventPipeEventMetaData>();
        Deserializer _deserializer;
        EventPipeFile _eventPipeFile;
        EventPipeTraceEventParser _eventParser; // TODO does this belong here?

        #endregion
    }

    unsafe class EventPipeEventMetaData
    {
        /// <summary>
        /// Creates a new MetaData instance from the serialized data at the current position of 'reader'
        /// of length 'length'.   This typically point at the PAYLOAD AREA of a meta-data events)
        /// 'fileFormatVersionNumber' is the version number of the file as a whole
        /// (since that affects the parsing of this data).   When this constructor returns the reader
        /// has read all data given to it (thus it has move the read pointer by 'length'.  
        public EventPipeEventMetaData(PinnedStreamReader reader, int length, int fileFormatVersionNumber)
        {
            StreamLabel eventDataEnd = reader.Current.Add(length);

            _eventRecord = (TraceEventNativeMethods.EVENT_RECORD*)Marshal.AllocHGlobal(sizeof(TraceEventNativeMethods.EVENT_RECORD));
            ClearMemory(_eventRecord, sizeof(TraceEventNativeMethods.EVENT_RECORD));

            StreamLabel metaDataStart = reader.Current;
            if (fileFormatVersionNumber == 1)
                _eventRecord->EventHeader.ProviderId = reader.ReadGuid();
            else
            {
                ProviderName = reader.ReadString();
                _eventRecord->EventHeader.ProviderId = GetProviderGuidFromProviderName(ProviderName);
            }

            var eventId = (ushort)reader.ReadInt32();
            _eventRecord->EventHeader.Id = eventId;
            Debug.Assert(_eventRecord->EventHeader.Id == eventId);  // No trucation

            var version = reader.ReadInt32();
            _eventRecord->EventHeader.Version = (byte)version;
            Debug.Assert(_eventRecord->EventHeader.Version == version);  // No trucation

            int metadataLength = reader.ReadInt32();
            Debug.Assert(0 <= metadataLength && metadataLength < length);
            if (0 < metadataLength)
            {
                // FIX NOW : We see to have two IDs and Version numbers.
                _eventRecord->EventHeader.Id = (ushort)reader.ReadInt32();
                EventName = reader.ReadString();

                // Deduce the opcode from the name.   
                if (EventName.EndsWith("Start", StringComparison.OrdinalIgnoreCase))
                    _eventRecord->EventHeader.Opcode = (byte)TraceEventOpcode.Start;
                else if (EventName.EndsWith("Stop", StringComparison.OrdinalIgnoreCase))
                    _eventRecord->EventHeader.Opcode = (byte)TraceEventOpcode.Stop;

                _eventRecord->EventHeader.Keyword = (ulong)reader.ReadInt64();
                _eventRecord->EventHeader.Level = (byte)reader.ReadInt32();

                int parameterCount = reader.ReadInt32();
                Debug.Assert(0 <= parameterCount, "Parameter count should not be negative.");
                if (parameterCount > 0)
                {
                    ParameterDefinitions = new Tuple<TypeCode, string>[parameterCount];
                    for (int i = 0; i < parameterCount; i++)
                    {
                        var type = (TypeCode)reader.ReadInt32();
                        var name = reader.ReadString();
                        ParameterDefinitions[i] = new Tuple<TypeCode, string>(type, name);
                    }
                }
            }
            Debug.Assert(reader.Current == eventDataEnd);
        }

        public TraceEventNativeMethods.EVENT_RECORD* GetEventRecordForEventData(EventPipeEventHeader* eventData, int pointerSize)
        {

            // We have already initialize all the fields of _eventRecord that do no vary from event to event. 
            // Now we only have to copy over the fields that are specific to particular event.  
            _eventRecord->EventHeader.ThreadId = eventData->ThreadId;
            _eventRecord->EventHeader.TimeStamp = eventData->TimeStamp;
            _eventRecord->EventHeader.ActivityId = eventData->ActivityID;
            // EVENT_RECORD does not field for ReleatedActivityID (because it is rarely used).  See GetRelatedActivityID;
            _eventRecord->UserDataLength = (ushort)eventData->PayloadSize;
            Debug.Assert(_eventRecord->UserDataLength == eventData->PayloadSize, "Payload size truncation!");
            _eventRecord->UserData = (IntPtr)eventData->Payload;

            int stackBytesSize = EventPipeEventHeader.StackBytesSize(eventData);

            // TODO remove once .NET Core has been fixed to not emit stacks on CLR method events which are just for bookeeping.  
            if (ProviderId == ClrRundownTraceEventParser.ProviderGuid ||
               (ProviderId == ClrTraceEventParser.ProviderGuid && (140 <= EventId && EventId <= 144 || EventId == 190)))     // These are various CLR method Events.  
                stackBytesSize = 0;

            if (0 < stackBytesSize)
            {
                // Lazy allocation (destructor frees it). 
                if (_eventRecord->ExtendedData == null)
                    _eventRecord->ExtendedData = (TraceEventNativeMethods.EVENT_HEADER_EXTENDED_DATA_ITEM*)Marshal.AllocHGlobal(sizeof(TraceEventNativeMethods.EVENT_HEADER_EXTENDED_DATA_ITEM));

                // Hook in the stack data.  
                _eventRecord->ExtendedData->ExtType = pointerSize == 4 ? TraceEventNativeMethods.EVENT_HEADER_EXT_TYPE_STACK_TRACE32 : TraceEventNativeMethods.EVENT_HEADER_EXT_TYPE_STACK_TRACE64;

                // DataPtr should point at a EVENT_EXTENDED_ITEM_STACK_TRACE*.  These have a ulong MatchID field which is NOT USED before the stack data.
                // Since that field is not used, I can backup the pointer by 8 bytes and synthesize a EVENT_EXTENDED_ITEM_STACK_TRACE from the raw buffer 
                // of stack data without having to copy.  
                _eventRecord->ExtendedData->DataSize = (ushort)(stackBytesSize + 8);
                _eventRecord->ExtendedData->DataPtr = (ulong)(EventPipeEventHeader.StackBytes(eventData) - 8);

                _eventRecord->ExtendedDataCount = 1;        // Mark that we have the stack data.  
            }
            else
                _eventRecord->ExtendedDataCount = 0;

            return _eventRecord;
        }

        public string ProviderName { get; private set; }
        public string EventName { get; private set; }
        public Tuple<TypeCode, string>[] ParameterDefinitions { get; private set; }
        public Guid ProviderId { get { return _eventRecord->EventHeader.ProviderId; } }
        public int EventId { get { return _eventRecord->EventHeader.Id; } }
        public int Version { get { return _eventRecord->EventHeader.Version; } }
        public ulong Keywords { get { return _eventRecord->EventHeader.Keyword; } }
        public int Level { get { return _eventRecord->EventHeader.Level; } }

        #region private 
        ~EventPipeEventMetaData()
        {
            if (_eventRecord != null)
            {
                if (_eventRecord->ExtendedData != null)
                    Marshal.FreeHGlobal((IntPtr)_eventRecord->ExtendedData);
                Marshal.FreeHGlobal((IntPtr)_eventRecord);
                _eventRecord = null;
            }

        }

        private void ClearMemory(void* buffer, int length)
        {
            byte* ptr = (byte*)buffer;
            while (length > 0)
            {
                *ptr++ = 0;
                --length;
            }

        }
        public static Guid GetProviderGuidFromProviderName(string name)
        {
            if (String.IsNullOrEmpty(name))
            {
                return Guid.Empty;
            }

            // Legacy GUID lookups (events which existed before the current Guid generation conventions)
            if (name == TplEtwProviderTraceEventParser.ProviderName)
            {
                return TplEtwProviderTraceEventParser.ProviderGuid;
            }
            else if (name == ClrTraceEventParser.ProviderName)
            {
                return ClrTraceEventParser.ProviderGuid;
            }
            else if (name == ClrPrivateTraceEventParser.ProviderName)
            {
                return ClrPrivateTraceEventParser.ProviderGuid;
            }
            else if (name == ClrRundownTraceEventParser.ProviderName)
            {
                return ClrRundownTraceEventParser.ProviderGuid;
            }
            else if (name == ClrStressTraceEventParser.ProviderName)
            {
                return ClrStressTraceEventParser.ProviderGuid;
            }
            else if (name == FrameworkEventSourceTraceEventParser.ProviderName)
            {
                return FrameworkEventSourceTraceEventParser.ProviderGuid;
            }
            // Needed as long as eventpipeinstance v1 objects are supported
            else if (name == SampleProfilerTraceEventParser.ProviderName)
            {
                return SampleProfilerTraceEventParser.ProviderGuid;
            }

            // Hash the name according to current event source naming conventions
            else
            {
                return TraceEventProviders.GetEventSourceGuidFromName(name);
            }
        }


        TraceEventNativeMethods.EVENT_RECORD* _eventRecord;
        #endregion
    }

}

namespace Microsoft.DotNet.Runtime
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct EventPipeEventHeader
    {
        public int EventSize;           // Size bytes of this header and the payload (but does NOT include the Stack (size and bytes))).  
        public StreamLabel MetaDataId;  // a number identifying the description of this event.  It is a stream location. 
        public int ThreadId;
        public long TimeStamp;
        public Guid ActivityID;
        public Guid RelatedActivityID;
        public int PayloadSize;         // size in bytes of the user defined payload data. 
        public fixed byte Payload[4];     // Actually of variable size.  4 is used to avoid potential alignment issues.   This 4 also appears in HeaderSize below. 

        /// <summary>
        /// Header Size is defined to be the number of bytes before the Payload bytes.  
        /// </summary>
        static public int HeaderSize { get { return sizeof(EventPipeEventHeader) - 4; } }
        static public EventPipeEventHeader* HeaderFromPayloadPointer(byte* payloadPtr) { return (EventPipeEventHeader*)(payloadPtr - HeaderSize); }

        static public int StackBytesSize(EventPipeEventHeader* header)
        {
            return *((int*)(&header->Payload[header->PayloadSize]));
        }
        static public byte* StackBytes(EventPipeEventHeader* header)
        {
            return (byte*)(&header->Payload[header->PayloadSize + 4]);
        }
    }

    class EventPipeFile : IFastSerializable
    {
        #region private 
        public void FromStream(Deserializer deserializer)
        {
            _version = deserializer.VersionBeingRead;

            ForwardReference reference = deserializer.ReadForwardReference();
            _endOfEventStream = deserializer.ResolveForwardReference(reference, preserveCurrent: true);

            // The start time is stored as a SystemTime which is a bunch of shorts, convert to DateTime.  
            short year = deserializer.ReadInt16();
            short month = deserializer.ReadInt16();
            short dayOfWeek = deserializer.ReadInt16();
            short day = deserializer.ReadInt16();
            short hour = deserializer.ReadInt16();
            short minute = deserializer.ReadInt16();
            short second = deserializer.ReadInt16();
            short milliseconds = deserializer.ReadInt16();
            _syncTimeUTC = new DateTime(year, month, day, hour, minute, second, milliseconds, DateTimeKind.Utc);

            deserializer.Read(out _syncTimeQPC);
            deserializer.Read(out _QPCFreq);

            _startOfStream = deserializer.Current;      // Events immediately after the header.  
        }

        public void ToStream(Serializer serializer)
        {
            // Managed code does not make a EventPipe today so we don't need to implement the serialization code.  
            throw new NotImplementedException();
        }

        internal int _version;
        internal StreamLabel _startOfStream;
        internal StreamLabel _endOfEventStream;

        DateTime _syncTimeUTC;
        long _syncTimeQPC;
        long _QPCFreq;
        #endregion
    }
}

#if !OLD
namespace Microsoft.Diagnostics.Tracing.EventPipe
{
    public abstract class EventPipeEventSource : TraceEventDispatcher
    {
        public EventPipeEventSource(Deserializer deserializer)
        {
            if (deserializer == null)
            {
                throw new ArgumentNullException(nameof(deserializer));
            }

            _deserializer = deserializer;
        }

        ~EventPipeEventSource()
        {
            Dispose(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _deserializer?.Dispose();
            }

            base.Dispose(disposing);
            GC.SuppressFinalize(this);
        }

        #region Private
        protected Deserializer _deserializer;
        #endregion

    }
}
#endif