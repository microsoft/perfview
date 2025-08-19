using Microsoft.Diagnostics.Tracing.EventPipe;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Tracing
{
    internal enum EventPipeFieldLayoutVersion
    {
        FileFormatV5OrLess = 1,
        FileFormatV5OptionalParamsTag = 2,
        FileFormatV6OrGreater = 3
    }

    internal enum EventPipeMetadataTag
    {
        Opcode = 1,
        ParameterPayloadV2 = 2
    }

    /// <summary>
    /// An EventPipeEventMetadata holds the information that can be shared among all
    /// instances of an event from a particular provider. Thus it contains
    /// things like the event name, provider, parameter names and types.
    ///
    /// This class has two main functions
    ///    1. It has parsing functions to read the metadata from the serialized stream.
    ///    2. It remembers a EVENT_RECORD structure (from ETW) that contains this data
    ///       and has a function GetEventRecordForEventData which converts from a
    ///       EventPipeEventHeader (the raw serialized data) to a EVENT_RECORD (which
    ///       is what TraceEvent needs to look up the event an pass it up the stack).
    /// </summary>
    internal unsafe class EventPipeMetadata
    {
        /// <summary>
        /// The SpanReader should cover exactly the event payload area for a metadata containing event.
        /// </summary>
        public static EventPipeMetadata ReadV5OrLower(ref SpanReader reader, int pointerSize, int processId, int fileFormatVersionNumber)
        {
            // Read in the header (The header does not include payload parameter information)
            var metadata = new EventPipeMetadata(pointerSize, processId);
            metadata.ParseHeader(ref reader, fileFormatVersionNumber);

            // If the metadata contains no parameter metadata, don't attempt to read it.
            if (reader.RemainingBytes.Length == 0)
            {
                metadata.InitDefaultParameters();
            }
            else
            {
                metadata.ParseEventParametersV5OrLess(ref reader, EventPipeFieldLayoutVersion.FileFormatV5OrLess);
            }

            while (reader.RemainingBytes.Length > 0)
            {
                // If we've already parsed the V1 metadata and there's more left to decode,
                // then we have some tags to read
                int tagLength = reader.ReadInt32();
                EventPipeMetadataTag tag = (EventPipeMetadataTag)reader.ReadUInt8();
                long offset = reader.StreamOffset;
                SpanReader tagReader = new SpanReader(reader.ReadBytes(tagLength), offset);

                if (tag == EventPipeMetadataTag.ParameterPayloadV2)
                {
                    metadata.ParseEventParametersV5OrLess(ref tagReader, EventPipeFieldLayoutVersion.FileFormatV5OptionalParamsTag);
                }
                else if (tag == EventPipeMetadataTag.Opcode)
                {
                    metadata.Opcode = tagReader.ReadUInt8();
                }
            }

            metadata.ApplyTransforms();
            return metadata;
        }

        /// <summary>
        /// The SpanReader should be positioned at the at the beginning of a V6 metadata blob.
        /// </summary>
        public static EventPipeMetadata ReadV6OrGreater(ref SpanReader reader, int pointerSize)
        {
            var metadata = new EventPipeMetadata(pointerSize);
            int metadataLength = reader.ReadUInt16();
            long offset = reader.StreamOffset;
            SpanReader metadataReader = new SpanReader(reader.ReadBytes(metadataLength), offset);
            metadata.ReadHeaderV6OrGreater(ref metadataReader);
            metadata.ParseEventParametersV6OrGreater(ref metadataReader);
            metadata.ParseOptionalMetadataV6OrGreater(ref metadataReader);
            metadata.ApplyTransforms();
            return metadata;
        }

        private EventPipeMetadata(int pointerSize)
            : this(pointerSize, -1)
        {
        }

        /// <summary>
        /// 'processID' is the process ID for the whole stream or -1 is this is a V6+ stream that can support multiple processes.
        /// </summary>
        private EventPipeMetadata(int pointerSize, int processId)
        {
            // Get the event record and fill in fields that we can without deserializing anything.
            _eventRecord = (TraceEventNativeMethods.EVENT_RECORD*)Marshal.AllocHGlobal(sizeof(TraceEventNativeMethods.EVENT_RECORD));
            ClearMemory(_eventRecord, sizeof(TraceEventNativeMethods.EVENT_RECORD));
            _extendedDataBuffer = (TraceEventNativeMethods.EVENT_HEADER_EXTENDED_DATA_ITEM*)Marshal.AllocHGlobal(2*sizeof(TraceEventNativeMethods.EVENT_HEADER_EXTENDED_DATA_ITEM));
            ClearMemory(_extendedDataBuffer, 2 * sizeof(TraceEventNativeMethods.EVENT_HEADER_EXTENDED_DATA_ITEM));
            _relatedActivityBuffer = (Guid*)Marshal.AllocHGlobal(sizeof(Guid));
            ClearMemory(_relatedActivityBuffer, sizeof(Guid));

            if (pointerSize == 4)
            {
                _eventRecord->EventHeader.Flags = TraceEventNativeMethods.EVENT_HEADER_FLAG_32_BIT_HEADER;
            }
            else
            {
                _eventRecord->EventHeader.Flags = TraceEventNativeMethods.EVENT_HEADER_FLAG_64_BIT_HEADER;
            }

            _eventRecord->EventHeader.ProcessId = processId;
        }

        ~EventPipeMetadata()
        {
            Marshal.FreeHGlobal((IntPtr)_extendedDataBuffer);
            _extendedDataBuffer = null;
            Marshal.FreeHGlobal((IntPtr)_relatedActivityBuffer);
            _relatedActivityBuffer = null;
            Marshal.FreeHGlobal((IntPtr)_eventRecord);
            _eventRecord = null;
        }

        public void ParseHeader(ref SpanReader reader, int fileFormatVersion)
        {
            if (fileFormatVersion >= 3)
            {
                ReadMetadataHeaderV3ToV5(ref reader);
            }
#if SUPPORT_V1_V2
            else
            {
                ReadObsoleteEventMetaData(ref reader, fileFormatVersion);
            }
#endif
        }

        private void InitDefaultParameters()
        {
            ParameterNames = new string[0];
            ParameterTypes = new DynamicTraceEventData.PayloadFetch[0];
        }

        /// <summary>
        /// Given a EventPipeEventHeader takes a EventPipeEventHeader that is specific to an event, copies it
        /// on top of the static information in its EVENT_RECORD which is specialized meta-data
        /// and returns a pointer to it.  Thus this makes the EventPipe look like an ETW provider from
        /// the point of view of the upper level TraceEvent logic.
        /// </summary>
        internal TraceEventNativeMethods.EVENT_RECORD* GetEventRecordForEventData(in EventPipeEventHeader eventData)
        {
            // We have already initialize all the fields of _eventRecord that do not vary from event to event.
            // Now we only have to copy over the fields that are specific to particular event.

            // these events usually come from metadata, but they can be overridden by the label list
            _eventRecord->EventHeader.Opcode = eventData.OpCodeOverride ?? Opcode ?? 0;
            _eventRecord->EventHeader.Level = eventData.LevelOverride ?? Level;
            _eventRecord->EventHeader.Keyword = eventData.KeywordsOverride ?? Keywords;
            _eventRecord->EventHeader.Version = eventData.VersionOverride ?? EventVersion;

            // Note: ThreadId isn't 32 bit on all of our platforms but ETW EVENT_RECORD* only has room for a 32 bit
            // ID. We'll need to refactor up the stack if we want to expose a bigger ID.
            _eventRecord->EventHeader.ThreadId = unchecked((int)eventData.ThreadId);
            _eventRecord->EventHeader.ProcessId = unchecked((int)eventData.ProcessId);
            if (eventData.ThreadIndexOrId == eventData.CaptureThreadIndexOrId && eventData.CaptureProcNumber != -1)
            {
                // Its not clear how the caller is supposed to distinguish between events that we know were on
                // processor 0 vs. lacking information about what processor number the thread is on and
                // reporting 0. We could certainly change the API to make this more apparent, but for now I
                // am only focused on ensuring the data is in the file format and we could improve access in the
                // future.
                _eventRecord->BufferContext.ProcessorNumber = (byte)eventData.CaptureProcNumber;
            }
            _eventRecord->EventHeader.TimeStamp = eventData.TimeStamp;
            _eventRecord->EventHeader.ActivityId = eventData.ActivityID;
            // EVENT_RECORD does not field for ReleatedActivityID (because it is rarely used).  See GetRelatedActivityID;
            _eventRecord->UserDataLength = (ushort)eventData.PayloadSize;

            // TODO the extra || operator is a hack because the runtime actually tries to emit events that
            // exceed this for the GC/BulkSurvivingObjectRanges (event id == 21).  We suppress that assert
            // for now but this is a real bug in the runtime's event logging.  ETW can't handle payloads > 64K.
            Debug.Assert(_eventRecord->UserDataLength == eventData.PayloadSize ||
                _eventRecord->EventHeader.ProviderId == ClrTraceEventParser.ProviderGuid && _eventRecord->EventHeader.Id == 21);
            _eventRecord->UserData = eventData.Payload;

            int stackBytesSize = eventData.StackBytesSize;

            // TODO remove once .NET Core has been fixed to not emit stacks on CLR method events which are just for bookkeeping.
            if (ProviderId == ClrRundownTraceEventParser.ProviderGuid ||
               (ProviderId == ClrTraceEventParser.ProviderGuid && (140 <= EventId && EventId <= 144 || EventId == 190)))     // These are various CLR method Events.
            {
                stackBytesSize = 0;
            }

            if (0 == stackBytesSize && eventData.RelatedActivityID == Guid.Empty)
            {
                _eventRecord->ExtendedData = null;
                _eventRecord->ExtendedDataCount = 0;
            }
            else
            {
                ushort extendedDataCount = 0;
                TraceEventNativeMethods.EVENT_HEADER_EXTENDED_DATA_ITEM* curExtendedBufferPtr = _extendedDataBuffer;
                _eventRecord->ExtendedData = _extendedDataBuffer;
                
                if (stackBytesSize > 0)
                {
                    if ((_eventRecord->EventHeader.Flags & TraceEventNativeMethods.EVENT_HEADER_FLAG_32_BIT_HEADER) != 0)
                    {
                        curExtendedBufferPtr->ExtType = TraceEventNativeMethods.EVENT_HEADER_EXT_TYPE_STACK_TRACE32;
                    }
                    else
                    {
                        curExtendedBufferPtr->ExtType = TraceEventNativeMethods.EVENT_HEADER_EXT_TYPE_STACK_TRACE64;
                    }

                    // DataPtr should point at a EVENT_EXTENDED_ITEM_STACK_TRACE*.  These have a ulong MatchID field which is NOT USED before the stack data.
                    // Since that field is not used, I can backup the pointer by 8 bytes and synthesize a EVENT_EXTENDED_ITEM_STACK_TRACE from the raw buffer
                    // of stack data without having to copy.
                    curExtendedBufferPtr->DataSize = (ushort)(stackBytesSize + 8);
                    curExtendedBufferPtr->DataPtr = (ulong)(eventData.StackBytes - 8);
                    curExtendedBufferPtr++;
                    extendedDataCount++;
                }
                if (eventData.RelatedActivityID != Guid.Empty)
                {
                    curExtendedBufferPtr->ExtType = TraceEventNativeMethods.EVENT_HEADER_EXT_TYPE_RELATED_ACTIVITYID;
                    curExtendedBufferPtr->DataSize = (ushort)sizeof(Guid);
                    curExtendedBufferPtr->DataPtr = (ulong)_relatedActivityBuffer;
                    *_relatedActivityBuffer = eventData.RelatedActivityID;
                    curExtendedBufferPtr++;
                    extendedDataCount++;
                }
                _eventRecord->ExtendedDataCount = extendedDataCount;
            }

            return _eventRecord;
        }

        /// <summary>
        /// This is a number that is unique to this meta-data blob.  It is expected to be a small integer
        /// that starts at 1 (since 0 is reserved) and increases from there (thus an array can be used).
        /// It is what is matched up with EventPipeEventHeader.MetaDataId
        /// </summary>
        public int MetaDataId { get; internal set; }
        public string ProviderName { get; internal set; }
        public string EventName { get; private set; }
        public string MessageTemplate { get; private set; }
        public string Description { get; private set; }
        public Dictionary<string, string> Attributes { get; private set; } = new Dictionary<string, string>();
        public Guid ProviderId { get { return _eventRecord->EventHeader.ProviderId; } private set { _eventRecord->EventHeader.ProviderId = value; } }
        public int EventId { get { return _eventRecord->EventHeader.Id; } private set { _eventRecord->EventHeader.Id = (ushort)value; } }
        public byte EventVersion { get; private set; }
        public ulong Keywords { get; private set; }
        public byte Level { get; private set; }
        public byte? Opcode { get; private set; }

        public DynamicTraceEventData.PayloadFetch[] ParameterTypes { get; internal set; }
        public string[] ParameterNames { get; internal set; }

        private void ReadHeaderV6OrGreater(ref SpanReader reader)
        {
            MetaDataId = (int)reader.ReadVarUInt32();
            ProviderName = reader.ReadVarUIntUTF8String();
            EventId = (int)reader.ReadVarUInt32();
            EventName = reader.ReadVarUIntUTF8String();
        }

        /// <summary>
        /// Reads the meta data for information specific to one event.
        /// </summary>
        private void ReadMetadataHeaderV3ToV5(ref SpanReader reader)
        {
            MetaDataId = reader.ReadInt32();
            ProviderName = reader.ReadNullTerminatedUTF16String();
            ReadMetadataCommon(ref reader);
        }

        private void ReadMetadataCommon(ref SpanReader reader)
        {
            EventId = (ushort)reader.ReadInt32();
            EventName = reader.ReadNullTerminatedUTF16String();
            Keywords = (ulong)reader.ReadInt64();
            EventVersion = (byte)reader.ReadInt32();
            Level = (byte)reader.ReadInt32();
        }

#if SUPPORT_V1_V2
        private void ReadObsoleteEventMetaData(ref SpanReader reader, int fileFormatVersion)
        {
            Debug.Assert(fileFormatVersion <= 2);

            // Old versions use the stream offset as the MetaData ID, but the reader has advanced to the payload so undo it.
            MetaDataId = ((int)reader.StreamOffset) - EventPipeEventHeader.GetHeaderSize(fileFormatVersion);

            if (fileFormatVersion == 1)
            {
                ProviderId = reader.Read<Guid>();
            }
            else
            {
                ProviderName = reader.ReadNullTerminatedUTF16String();
            }

            EventId = (ushort)reader.ReadInt32();
            EventVersion = (byte)reader.ReadInt32();
            int metadataLength = reader.ReadInt32();
            if (0 < metadataLength)
            {
                ReadMetadataCommon(ref reader);
            }
        }
#endif

        public void ParseEventParametersV6OrGreater(ref SpanReader reader)
        {
            // Recursively parse the metadata, building up a list of payload names and payload field fetch objects.
            // V5 has a try/catch to handle unparsable metadata, but V6 intentionally does not. Each field has a leading
            // size field allowing some extensibility by smuggling extra information but it has to be optional.
            DynamicTraceEventData.PayloadFetchClassInfo classInfo = ParseFields(ref reader, EventPipeFieldLayoutVersion.FileFormatV6OrGreater);

            ParameterNames = classInfo.FieldNames;
            ParameterTypes = classInfo.FieldFetches;
        }

        /// <summary>
        /// Given the EventPipe metaData header and a stream pointing at the serialized meta-data for the parameters for the
        /// event, create a new  DynamicTraceEventData that knows how to parse that event.
        /// ReaderForParameters.Current is advanced past the parameter information.
        /// </summary>
        public void ParseEventParametersV5OrLess(ref SpanReader reader, EventPipeFieldLayoutVersion fieldLayoutVersion)
        {
            DynamicTraceEventData.PayloadFetchClassInfo classInfo = null;

            try
            {
                // Recursively parse the metadata, building up a list of payload names and payload field fetch objects.
                classInfo = ParseFields(ref reader, fieldLayoutVersion);
            }
            catch (FormatException)
            {
                // If we encounter unparsable metadata, ignore the payloads of this event type but don't fail to parse the entire
                // trace. This gives us more flexibility in the future to introduce new descriptive information.
                classInfo = new DynamicTraceEventData.PayloadFetchClassInfo()
                {
                    FieldNames = new string[0],
                    FieldFetches = new DynamicTraceEventData.PayloadFetch[0]
                };
            }

            ParameterNames = classInfo.FieldNames;
            ParameterTypes = classInfo.FieldFetches;
        }

        private DynamicTraceEventData.PayloadFetchClassInfo ParseFields(ref SpanReader reader, EventPipeFieldLayoutVersion fieldLayoutVersion)
        {
            int numFields = 0;
            if (fieldLayoutVersion >= EventPipeFieldLayoutVersion.FileFormatV6OrGreater)
            {
                numFields = reader.ReadUInt16();
            }
            else
            {
                numFields = reader.ReadInt32();
            }

            string[] fieldNames = new string[numFields];
            DynamicTraceEventData.PayloadFetch[] fieldFetches = new DynamicTraceEventData.PayloadFetch[numFields];

            ushort offset = 0;
            for (int fieldIndex = 0; fieldIndex < numFields; fieldIndex++)
            {
                string fieldName = "<unknown_field>";
                DynamicTraceEventData.PayloadFetch payloadFetch;
                if (fieldLayoutVersion >= EventPipeFieldLayoutVersion.FileFormatV6OrGreater)
                {
                    int fieldLength = reader.ReadUInt16();
                    long streamOffset = reader.StreamOffset;
                    SpanReader fieldReader = new SpanReader(reader.ReadBytes(fieldLength), streamOffset);
                    fieldName = fieldReader.ReadVarUIntUTF8String();
                    payloadFetch = ParseType(ref fieldReader, offset, fieldName, fieldLayoutVersion);
                }
                else
                if (fieldLayoutVersion >= EventPipeFieldLayoutVersion.FileFormatV5OptionalParamsTag)
                {
                    int fieldLength = reader.ReadInt32();
                    long streamOffset = reader.StreamOffset;
                    SpanReader fieldReader = new SpanReader(reader.ReadBytes(fieldLength - 4), streamOffset);
                    fieldName = fieldReader.ReadNullTerminatedUTF16String();
                    payloadFetch = ParseType(ref fieldReader, offset, fieldName, fieldLayoutVersion);
                }
                else
                {
                    payloadFetch = ParseType(ref reader, offset, fieldName, fieldLayoutVersion);
                    fieldName = reader.ReadNullTerminatedUTF16String();
                }

                fieldNames[fieldIndex] = fieldName;

                // Update the offset into the event for the next payload fetch.
                if (payloadFetch.Size >= DynamicTraceEventData.SPECIAL_SIZES || offset == ushort.MaxValue)
                {
                    offset = ushort.MaxValue;           // Indicate that the offset must be computed at run time.
                }
                else
                {
                    offset += payloadFetch.Size;
                }

                // Save the current payload fetch.
                fieldFetches[fieldIndex] = payloadFetch;
            }

            return new DynamicTraceEventData.PayloadFetchClassInfo()
            {
                FieldNames = fieldNames,
                FieldFetches = fieldFetches
            };
        }

        private DynamicTraceEventData.PayloadFetch ParseType(
            ref SpanReader reader,
            ushort offset,
            string fieldName,
            EventPipeFieldLayoutVersion fieldLayoutVersion)
        {
            DynamicTraceEventData.PayloadFetch payloadFetch = new DynamicTraceEventData.PayloadFetch();

            // Read the TypeCode for the current field.
            EventPipeTypeCode typeCode = 0;
            if (fieldLayoutVersion >= EventPipeFieldLayoutVersion.FileFormatV6OrGreater)
            {
                typeCode = (EventPipeTypeCode)reader.ReadUInt8();
            }
            else
            {
                typeCode = (EventPipeTypeCode)reader.ReadInt32();
            }

            // Fill out the payload fetch object based on the TypeCode.
            switch (typeCode)
            {
                case EventPipeTypeCode.Boolean:
                    {
                        payloadFetch.Type = typeof(bool);
                        payloadFetch.Size = 4; // We follow windows conventions and use 4 bytes for bool.
                        payloadFetch.Offset = offset;
                        break;
                    }
                case EventPipeTypeCode.UTF16CodeUnit:
                    {
                        payloadFetch.Type = typeof(char);
                        payloadFetch.Size = sizeof(char);
                        payloadFetch.Offset = offset;
                        break;
                    }
                case EventPipeTypeCode.SByte:
                    {
                        payloadFetch.Type = typeof(SByte);
                        payloadFetch.Size = sizeof(SByte);
                        payloadFetch.Offset = offset;
                        break;
                    }
                case EventPipeTypeCode.Byte:
                    {
                        payloadFetch.Type = typeof(byte);
                        payloadFetch.Size = sizeof(byte);
                        payloadFetch.Offset = offset;
                        break;
                    }
                case EventPipeTypeCode.Int16:
                    {
                        payloadFetch.Type = typeof(Int16);
                        payloadFetch.Size = sizeof(Int16);
                        payloadFetch.Offset = offset;
                        break;
                    }
                case EventPipeTypeCode.UInt16:
                    {
                        payloadFetch.Type = typeof(UInt16);
                        payloadFetch.Size = sizeof(UInt16);
                        payloadFetch.Offset = offset;
                        break;
                    }
                case EventPipeTypeCode.Int32:
                    {
                        payloadFetch.Type = typeof(Int32);
                        payloadFetch.Size = sizeof(Int32);
                        payloadFetch.Offset = offset;
                        break;
                    }
                case EventPipeTypeCode.UInt32:
                    {
                        payloadFetch.Type = typeof(UInt32);
                        payloadFetch.Size = sizeof(UInt32);
                        payloadFetch.Offset = offset;
                        break;
                    }
                case EventPipeTypeCode.Int64:
                    {
                        payloadFetch.Type = typeof(Int64);
                        payloadFetch.Size = sizeof(Int64);
                        payloadFetch.Offset = offset;
                        break;
                    }
                case EventPipeTypeCode.UInt64:
                    {
                        payloadFetch.Type = typeof(UInt64);
                        payloadFetch.Size = sizeof(UInt64);
                        payloadFetch.Offset = offset;
                        break;
                    }
                case EventPipeTypeCode.Single:
                    {
                        payloadFetch.Type = typeof(Single);
                        payloadFetch.Size = sizeof(Single);
                        payloadFetch.Offset = offset;
                        break;
                    }
                case EventPipeTypeCode.Double:
                    {
                        payloadFetch.Type = typeof(Double);
                        payloadFetch.Size = sizeof(Double);
                        payloadFetch.Offset = offset;
                        break;
                    }
                case EventPipeTypeCode.Decimal:
                    {
                        payloadFetch.Type = typeof(Decimal);
                        payloadFetch.Size = sizeof(Decimal);
                        payloadFetch.Offset = offset;
                        break;
                    }
                case EventPipeTypeCode.DateTime:
                    {
                        payloadFetch.Type = typeof(DateTime);
                        payloadFetch.Size = 8;
                        payloadFetch.Offset = offset;
                        break;
                    }
                case EventPipeTypeCode.Guid:
                    {
                        payloadFetch.Type = typeof(Guid);
                        payloadFetch.Size = 16;
                        payloadFetch.Offset = offset;
                        break;
                    }
                case EventPipeTypeCode.NullTerminatedUTF16String:
                    {
                        payloadFetch.Type = typeof(String);
                        payloadFetch.Size = DynamicTraceEventData.NULL_TERMINATED;
                        payloadFetch.Offset = offset;
                        break;
                    }
                case EventPipeTypeCode.Object:
                    {
                        // TypeCode.Object represents an embedded struct.
                        DynamicTraceEventData.PayloadFetchClassInfo embeddedStructClassInfo = ParseFields(ref reader, fieldLayoutVersion);
                        Debug.Assert(embeddedStructClassInfo != null);
                        payloadFetch = DynamicTraceEventData.PayloadFetch.StructPayloadFetch(offset, embeddedStructClassInfo);
                        break;
                    }

                case EventPipeTypeCode.Array:
                    {
                        if (fieldLayoutVersion < EventPipeFieldLayoutVersion.FileFormatV5OptionalParamsTag)
                        {
                            throw new FormatException($"Array is not a valid type code for this metadata.");
                        }

                        DynamicTraceEventData.PayloadFetch elementType = ParseType(ref reader, 0, fieldName, fieldLayoutVersion);
                        // This fetchSize marks the array as being prefixed with an unsigned 16 bit count of elements
                        ushort fetchSize = DynamicTraceEventData.COUNTED_SIZE + DynamicTraceEventData.ELEM_COUNT;
                        payloadFetch = DynamicTraceEventData.PayloadFetch.ArrayPayloadFetch(offset, elementType, fetchSize);
                        break;
                    }
                case EventPipeTypeCode.VarInt:
                    {
                        if (fieldLayoutVersion < EventPipeFieldLayoutVersion.FileFormatV6OrGreater)
                        {
                            throw new FormatException($"VarInt is not a valid type code for this metadata.");
                        }
                        payloadFetch.Type = typeof(long);
                        payloadFetch.Size = DynamicTraceEventData.VARINT;
                        payloadFetch.Offset = offset;
                        break;
                    }
                case EventPipeTypeCode.VarUInt:
                    {
                        if (fieldLayoutVersion < EventPipeFieldLayoutVersion.FileFormatV6OrGreater)
                        {
                            throw new FormatException($"VarUInt is not a valid type code for this metadata.");
                        }
                        payloadFetch.Type = typeof(ulong);
                        payloadFetch.Size = DynamicTraceEventData.VARINT;
                        payloadFetch.Offset = offset;
                        break;
                    }
                case EventPipeTypeCode.FixedLengthArray:
                    {
                        if (fieldLayoutVersion < EventPipeFieldLayoutVersion.FileFormatV6OrGreater)
                        {
                            throw new FormatException($"FixedLengthArray is not a valid type code for this metadata.");
                        }
                        DynamicTraceEventData.PayloadFetch elementType = ParseType(ref reader, 0, fieldName, fieldLayoutVersion);
                        ushort elementCount = reader.ReadUInt16();
                        try
                        {
                            payloadFetch = DynamicTraceEventData.PayloadFetch.FixedCountArrayPayloadFetch(offset, elementType, elementCount);
                        }
                        catch (ArgumentOutOfRangeException e)
                        {
                            throw new FormatException($"FixedLengthArray too large", e);
                        }
                        
                        break;
                    }
                case EventPipeTypeCode.UTF8CodeUnit:
                    {
                        if (fieldLayoutVersion < EventPipeFieldLayoutVersion.FileFormatV6OrGreater)
                        {
                            throw new FormatException($"UTF8CodeUnit is not a valid type code for this metadata.");
                        }
                        payloadFetch.Type = typeof(char);
                        payloadFetch.Size = sizeof(byte);
                        payloadFetch.Offset = offset;
                        break;
                    }
                case EventPipeTypeCode.RelLoc:
                    {
                        if (fieldLayoutVersion < EventPipeFieldLayoutVersion.FileFormatV6OrGreater)
                        {
                            throw new FormatException($"RelLoc is not a valid type code for this metadata.");
                        }
                        DynamicTraceEventData.PayloadFetch elementType = ParseType(ref reader, 0, fieldName, fieldLayoutVersion);
                        if(!elementType.IsFixedSize)
                        {
                            throw new FormatException($"RelLoc requires a fixed size element type.");
                        }
                        payloadFetch = DynamicTraceEventData.PayloadFetch.RelLocPayloadFetch(offset, elementType);
                        break;
                    }
                case EventPipeTypeCode.DataLoc:
                    {
                        if (fieldLayoutVersion < EventPipeFieldLayoutVersion.FileFormatV6OrGreater)
                        {
                            throw new FormatException($"DataLoc is not a valid type code for this metadata.");
                        }
                        DynamicTraceEventData.PayloadFetch elementType = ParseType(ref reader, 0, fieldName, fieldLayoutVersion);
                        if(!elementType.IsFixedSize)
                        {
                            throw new FormatException($"DataLoc requires a fixed size element type.");
                        }
                        payloadFetch = DynamicTraceEventData.PayloadFetch.DataLocPayloadFetch(offset, elementType);
                        break;
                    }
                default:
                    {
                        throw new FormatException($"Field {fieldName}: Typecode {typeCode} is not supported.");
                    }
            }

            return payloadFetch;
        }

        enum EventPipeTypeCode
        {
            Object = 1,                        // Concatenate together all of the encoded fields
            Boolean = 3,                       // A 4-byte LE integer with value 0=false and 1=true.  
            UTF16CodeUnit = 4,                 // a 2-byte UTF16 code unit
            SByte = 5,                         // 1-byte signed integer
            Byte = 6,                          // 1-byte unsigned integer
            Int16 = 7,                         // 2-byte signed LE integer
            UInt16 = 8,                        // 2-byte unsigned LE integer
            Int32 = 9,                         // 4-byte signed LE integer
            UInt32 = 10,                       // 4-byte unsigned LE integer
            Int64 = 11,                        // 8-byte signed LE integer
            UInt64 = 12,                       // 8-byte unsigned LE integer
            Single = 13,                       // 4-byte single-precision IEEE754 floating point value
            Double = 14,                       // 8-byte double-precision IEEE754 floating point value
            Decimal = 15,                      // 16-byte decimal value - TODO: I'm not sure this one has ever worked? I don't see any code in dynamic event parser that handles it.
            DateTime = 16,                     // Encoded as 8 concatenated Int16s representing year, month, dayOfWeek, day, hour, minute, second, and milliseconds.
            Guid = 17,                         // A 16-byte guid encoded as the concatenation of an Int32, 2 Int16s, and 8 Uint8s
            NullTerminatedUTF16String = 18,    // A string encoded with UTF16 characters and a 2-byte null terminator
            Array = 19,                        // New in V5 optional params: a UInt16 length-prefixed variable-sized array. Elements are encoded depending on the ElementType.
            VarInt = 20,                       // New in V6: variable-length signed integer with zig-zag encoding (same as protobuf)
            VarUInt = 21,                      // New in V6: variable-length unsigned integer (ULEB128)
            FixedLengthArray = 22,             // New in V6: A fixed-length array of elements. The length is determined by the metadata.
            UTF8CodeUnit = 23,                 // New in V6: A single UTF8 code unit (1 byte).
            RelLoc = 24,                       // New in V6: An array at a relative location within the payload. 
            DataLoc = 25                       // New in V6: An absolute data location within the payload.
        }

        private void ParseOptionalMetadataV6OrGreater(ref SpanReader reader)
        {
            ushort size = reader.ReadUInt16();
            long streamOffset = reader.StreamOffset;
            SpanReader optionalMetadataReader = new SpanReader(reader.ReadBytes(size), streamOffset);
            while(optionalMetadataReader.RemainingBytes.Length > 0)
            {
                OptionalMetadataKind kind = (OptionalMetadataKind)optionalMetadataReader.ReadUInt8();
                switch (kind)
                {
                    case OptionalMetadataKind.OpCode:
                    {
                        Opcode = optionalMetadataReader.ReadUInt8();
                        break;
                    }
                    case OptionalMetadataKind.Keyword:
                    {
                        Keywords = optionalMetadataReader.ReadUInt64();
                        break;
                    }
                    case OptionalMetadataKind.MessageTemplate:
                    {
                        MessageTemplate = optionalMetadataReader.ReadVarUIntUTF8String();
                        break;
                    }
                    case OptionalMetadataKind.Description:
                    {
                        Description = optionalMetadataReader.ReadVarUIntUTF8String();
                        break;
                    }
                    case OptionalMetadataKind.KeyValue:
                    {
                        string key = optionalMetadataReader.ReadVarUIntUTF8String();
                        string value = optionalMetadataReader.ReadVarUIntUTF8String();
                        Attributes[key] = value;
                        break;
                    }
                    case OptionalMetadataKind.ProviderGuid:
                    {
                        ProviderId = optionalMetadataReader.Read<Guid>();
                        break;
                    }
                    case OptionalMetadataKind.Level:
                    {
                        Level = optionalMetadataReader.ReadUInt8();
                        break;
                    }
                    case OptionalMetadataKind.Version:
                    {
                        EventVersion = optionalMetadataReader.ReadUInt8();
                        break;
                    }
                    default:
                    {
                        throw new FormatException($"Unknown optional metadata kind {kind}");
                    }
                }
            }
        }

        enum OptionalMetadataKind
        {
            OpCode = 1,
            // 2 is no longer used. In format V5 it was V2Params
            Keyword = 3,
            MessageTemplate = 4,
            Description = 5,
            KeyValue = 6,
            ProviderGuid = 7,
            Level = 8,
            Version = 9
        }

        // this is a memset implementation.  Note that we often use the trick of assigning a pointer to a struct to *ptr = default(Type);
        // Span.Clear also now does this.
        private static void ClearMemory(void* buffer, int length)
        {
            byte* ptr = (byte*)buffer;
            while (length > 0)
            {
                *ptr++ = 0;
                --length;
            }
        }

        // After metadata has been read we do a set of baked in transforms.
        // None of these transforms are part of the NetTrace file format, rather they are a combination of TraceEvent/EventSource specific conventions
        // and workarounds for well known events in historic scenarios where data was missing.
        public void ApplyTransforms()
        {
            //TraceEvent expects empty name to be canonicalized as null rather than ""
            if (EventName == "")
            {
                EventName = null; 
            }

            if(ProviderId == Guid.Empty)
            {
                ProviderId = GetProviderGuidFromProviderName(ProviderName);
            }

            // Older format versions weren't able to represent the parameter types for some important events.  This works around that issue
            // by creating metadata on the fly for well-known EventSources.
            // This transform expects event names that have not yet been modified by ExtractImpliedOpcode()
            if (ParameterNames.Length == 0 && ParameterTypes.Length == 0)
            {
                PopulateWellKnownEventParameters();
            }
            
            if (!Opcode.HasValue)
            {
                ExtractImpliedOpcode();
            }
            
            StripStartStopInEventName();
        }

        public static Guid GetProviderGuidFromProviderName(string name)
        {
            if (string.IsNullOrEmpty(name))
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
#if SUPPORT_V1_V2
            else if (name == SampleProfilerTraceEventParser.ProviderName)
            {
                return SampleProfilerTraceEventParser.ProviderGuid;
            }
#endif
            // Hash the name according to current event source naming conventions
            else
            {
                return TraceEventProviders.GetEventSourceGuidFromName(name);
            }
        }

        // The NetPerf and NetTrace V1 file formats were incapable of representing some event parameter types that EventSource and ETW support.
        // This works around that issue without requiring a runtime or format update for well-known EventSources that used the indescribable types.
        private void PopulateWellKnownEventParameters()
        {
            if (ProviderName == "Microsoft-Diagnostics-DiagnosticSource")
            {
                string eventName = EventName;

                if (eventName == "Event" ||
                   eventName == "Activity1Start" ||
                   eventName == "Activity1Stop" ||
                   eventName == "Activity2Start" ||
                   eventName == "Activity2Stop" ||
                   eventName == "RecursiveActivity1Start" ||
                   eventName == "RecursiveActivity1Stop")
                {
                    DynamicTraceEventData.PayloadFetch[] fieldFetches = new DynamicTraceEventData.PayloadFetch[3];
                    string[] fieldNames = new string[3];
                    fieldFetches[0].Type = typeof(string);
                    fieldFetches[0].Size = DynamicTraceEventData.NULL_TERMINATED;
                    fieldFetches[0].Offset = 0;
                    fieldNames[0] = "SourceName";

                    fieldFetches[1].Type = typeof(string);
                    fieldFetches[1].Size = DynamicTraceEventData.NULL_TERMINATED;
                    fieldFetches[1].Offset = ushort.MaxValue;
                    fieldNames[1] = "EventName";

                    DynamicTraceEventData.PayloadFetch[] keyValuePairFieldFetches = new DynamicTraceEventData.PayloadFetch[2];
                    string[] keyValuePairFieldNames = new string[2];
                    keyValuePairFieldFetches[0].Type = typeof(string);
                    keyValuePairFieldFetches[0].Size = DynamicTraceEventData.NULL_TERMINATED;
                    keyValuePairFieldFetches[0].Offset = 0;
                    keyValuePairFieldNames[0] = "Key";
                    keyValuePairFieldFetches[1].Type = typeof(string);
                    keyValuePairFieldFetches[1].Size = DynamicTraceEventData.NULL_TERMINATED;
                    keyValuePairFieldFetches[1].Offset = ushort.MaxValue;
                    keyValuePairFieldNames[1] = "Value";
                    DynamicTraceEventData.PayloadFetchClassInfo keyValuePairClassInfo = new DynamicTraceEventData.PayloadFetchClassInfo()
                    {
                        FieldFetches = keyValuePairFieldFetches,
                        FieldNames = keyValuePairFieldNames
                    };
                    DynamicTraceEventData.PayloadFetch argumentElementFetch = DynamicTraceEventData.PayloadFetch.StructPayloadFetch(0, keyValuePairClassInfo);
                    ushort fetchSize = DynamicTraceEventData.COUNTED_SIZE + DynamicTraceEventData.ELEM_COUNT;
                    fieldFetches[2] = DynamicTraceEventData.PayloadFetch.ArrayPayloadFetch(ushort.MaxValue, argumentElementFetch, fetchSize);
                    fieldNames[2] = "Arguments";


                    ParameterTypes = fieldFetches;
                    ParameterNames = fieldNames;
                }
            }
        }

        /// <summary>
        /// If the event doesn't have an explicit Opcode and the event name ends with "Start" or "Stop",
        /// then we set the Opcode to Start or Stop respectively.
        /// </summary>
        private void ExtractImpliedOpcode()
        {
            if (EventName != null)
            {
                if (EventName.EndsWith("Start", StringComparison.OrdinalIgnoreCase))
                {
                    Opcode = (int)TraceEventOpcode.Start;
                }
                else if (EventName.EndsWith("Stop", StringComparison.OrdinalIgnoreCase))
                {
                    Opcode = (int)TraceEventOpcode.Stop;
                }
            }
        }

        /// <summary>
        /// If the event has a Stop/Start opcode and also ends with the word "Start" or "Stop", then
        /// remove the "Start" or "Stop" from the event name.
        /// </summary>
        private void StripStartStopInEventName()
        {
            if (EventName != null)
            {
                if (Opcode == (int)TraceEventOpcode.Start && EventName.EndsWith("Start", StringComparison.OrdinalIgnoreCase))
                {
                    EventName = EventName.Substring(0, EventName.Length - 5);
                }
                else if (Opcode == (int)TraceEventOpcode.Stop && EventName.EndsWith("Stop", StringComparison.OrdinalIgnoreCase))
                {
                    EventName = EventName.Substring(0, EventName.Length - 4);
                }
            }
        }

        private TraceEventNativeMethods.EVENT_RECORD* _eventRecord;
        private TraceEventNativeMethods.EVENT_HEADER_EXTENDED_DATA_ITEM* _extendedDataBuffer;
        private Guid* _relatedActivityBuffer;
    }
}
