using Microsoft.Diagnostics.Tracing.EventPipe;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Tracing
{
    internal enum EventPipeFieldLayoutVersion
    {
        V1 = 1, // Used by V1 parameter blobs
        V2 = 2 // Use by V2 parameter blobs
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
        /// 'processID' is the process ID for the whole stream (since it needs to be put into the EVENT_RECORD.
        /// </summary>
        public EventPipeMetadata(int pointerSize, int processId)
        {
            // Get the event record and fill in fields that we can without deserializing anything.
            _eventRecord = (TraceEventNativeMethods.EVENT_RECORD*)Marshal.AllocHGlobal(sizeof(TraceEventNativeMethods.EVENT_RECORD));
            ClearMemory(_eventRecord, sizeof(TraceEventNativeMethods.EVENT_RECORD));

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
            if (_eventRecord != null)
            {
                if (_extendedDataBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_extendedDataBuffer);
                }

                Marshal.FreeHGlobal((IntPtr)_eventRecord);
                _eventRecord = null;
            }
        }

        public void ParseHeader(ref SpanReader reader, int fileFormatVersion)
        {
            if (fileFormatVersion >= 3)
            {
                ReadNetTraceMetadata(ref reader);
            }
#if SUPPORT_V1_V2
            else
            {
                ReadObsoleteEventMetaData(ref reader, fileFormatVersion);
            }
#endif
        }

        public void InitDefaultParameters()
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
            // We have already initialize all the fields of _eventRecord that do no vary from event to event.
            // Now we only have to copy over the fields that are specific to particular event.
            //
            // Note: ThreadId isn't 32 bit on all of our platforms but ETW EVENT_RECORD* only has room for a 32 bit
            // ID. We'll need to refactor up the stack if we want to expose a bigger ID.
            _eventRecord->EventHeader.ThreadId = unchecked((int)eventData.ThreadId);
            if (eventData.ThreadId == eventData.CaptureThreadId && eventData.CaptureProcNumber != -1)
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

            if (0 < stackBytesSize)
            {
                // Lazy allocation (destructor frees it).
                if (_extendedDataBuffer == IntPtr.Zero)
                {
                    _extendedDataBuffer = Marshal.AllocHGlobal(sizeof(TraceEventNativeMethods.EVENT_HEADER_EXTENDED_DATA_ITEM));
                }

                _eventRecord->ExtendedData = (TraceEventNativeMethods.EVENT_HEADER_EXTENDED_DATA_ITEM*)_extendedDataBuffer;

                if ((_eventRecord->EventHeader.Flags & TraceEventNativeMethods.EVENT_HEADER_FLAG_32_BIT_HEADER) != 0)
                {
                    _eventRecord->ExtendedData->ExtType = TraceEventNativeMethods.EVENT_HEADER_EXT_TYPE_STACK_TRACE32;
                }
                else
                {
                    _eventRecord->ExtendedData->ExtType = TraceEventNativeMethods.EVENT_HEADER_EXT_TYPE_STACK_TRACE64;
                }

                // DataPtr should point at a EVENT_EXTENDED_ITEM_STACK_TRACE*.  These have a ulong MatchID field which is NOT USED before the stack data.
                // Since that field is not used, I can backup the pointer by 8 bytes and synthesize a EVENT_EXTENDED_ITEM_STACK_TRACE from the raw buffer
                // of stack data without having to copy.
                _eventRecord->ExtendedData->DataSize = (ushort)(stackBytesSize + 8);
                _eventRecord->ExtendedData->DataPtr = (ulong)(eventData.StackBytes - 8);

                _eventRecord->ExtendedDataCount = 1;        // Mark that we have the stack data.
            }
            else
            {
                _eventRecord->ExtendedDataCount = 0;
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
        public Guid ProviderId { get { return _eventRecord->EventHeader.ProviderId; } private set { _eventRecord->EventHeader.ProviderId = value; } }
        public int EventId { get { return _eventRecord->EventHeader.Id; } private set { _eventRecord->EventHeader.Id = (ushort)value; } }
        public int EventVersion { get { return _eventRecord->EventHeader.Version; } private set { _eventRecord->EventHeader.Version = (byte)value; } }
        public ulong Keywords { get { return _eventRecord->EventHeader.Keyword; } private set { _eventRecord->EventHeader.Keyword = value; } }
        public int Level { get { return _eventRecord->EventHeader.Level; } private set { _eventRecord->EventHeader.Level = (byte)value; } }
        public byte Opcode { get { return _eventRecord->EventHeader.Opcode; } internal set { _eventRecord->EventHeader.Opcode = (byte)value; } }

        public DynamicTraceEventData.PayloadFetch[] ParameterTypes { get; internal set; }
        public string[] ParameterNames { get; internal set; }

        /// <summary>
        /// Reads the meta data for information specific to one event.
        /// </summary>
        private void ReadNetTraceMetadata(ref SpanReader reader)
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


        /// <summary>
        /// Given the EventPipe metaData header and a stream pointing at the serialized meta-data for the parameters for the
        /// event, create a new  DynamicTraceEventData that knows how to parse that event.
        /// ReaderForParameters.Current is advanced past the parameter information.
        /// </summary>
        public void ParseEventParameters(ref SpanReader reader, EventPipeFieldLayoutVersion fieldLayoutVersion)
        {
            DynamicTraceEventData.PayloadFetchClassInfo classInfo = null;

            // Read the count of event payload fields.
            int fieldCount = reader.ReadInt32();
            Debug.Assert(0 <= fieldCount && fieldCount < 0x4000);

            if (fieldCount > 0)
            {
                try
                {
                    // Recursively parse the metadata, building up a list of payload names and payload field fetch objects.
                    classInfo = ParseFields(ref reader, fieldCount, fieldLayoutVersion);
                }
                catch (FormatException)
                {
                    // If we encounter unparsable metadata, ignore the payloads of this event type but don't fail to parse the entire
                    // trace. This gives us more flexibility in the future to introduce new descriptive information.
                    classInfo = null;
                }
            }

            if (classInfo == null)
            {
                classInfo = new DynamicTraceEventData.PayloadFetchClassInfo()
                {
                    FieldNames = new string[0],
                    FieldFetches = new DynamicTraceEventData.PayloadFetch[0]
                };
            }

            ParameterNames = classInfo.FieldNames;
            ParameterTypes = classInfo.FieldFetches;
        }

        private DynamicTraceEventData.PayloadFetchClassInfo ParseFields(ref SpanReader reader, int numFields, EventPipeFieldLayoutVersion fieldLayoutVersion)
        {
            string[] fieldNames = new string[numFields];
            DynamicTraceEventData.PayloadFetch[] fieldFetches = new DynamicTraceEventData.PayloadFetch[numFields];

            ushort offset = 0;
            for (int fieldIndex = 0; fieldIndex < numFields; fieldIndex++)
            {
                string fieldName = "<unknown_field>";
                DynamicTraceEventData.PayloadFetch payloadFetch;
                if (fieldLayoutVersion >= EventPipeFieldLayoutVersion.V2)
                {
                    long fieldLength = reader.ReadInt32();
                    long streamOffset = reader.StreamOffset;
                    SpanReader fieldReader = new SpanReader(reader.ReadBytes((int)fieldLength - 4), streamOffset);
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
            TypeCode typeCode = (TypeCode)reader.ReadInt32();

            // Fill out the payload fetch object based on the TypeCode.
            switch (typeCode)
            {
                case TypeCode.Boolean:
                    {
                        payloadFetch.Type = typeof(bool);
                        payloadFetch.Size = 4; // We follow windows conventions and use 4 bytes for bool.
                        payloadFetch.Offset = offset;
                        break;
                    }
                case TypeCode.Char:
                    {
                        payloadFetch.Type = typeof(char);
                        payloadFetch.Size = sizeof(char);
                        payloadFetch.Offset = offset;
                        break;
                    }
                case TypeCode.SByte:
                    {
                        payloadFetch.Type = typeof(SByte);
                        payloadFetch.Size = sizeof(SByte);
                        payloadFetch.Offset = offset;
                        break;
                    }
                case TypeCode.Byte:
                    {
                        payloadFetch.Type = typeof(byte);
                        payloadFetch.Size = sizeof(byte);
                        payloadFetch.Offset = offset;
                        break;
                    }
                case TypeCode.Int16:
                    {
                        payloadFetch.Type = typeof(Int16);
                        payloadFetch.Size = sizeof(Int16);
                        payloadFetch.Offset = offset;
                        break;
                    }
                case TypeCode.UInt16:
                    {
                        payloadFetch.Type = typeof(UInt16);
                        payloadFetch.Size = sizeof(UInt16);
                        payloadFetch.Offset = offset;
                        break;
                    }
                case TypeCode.Int32:
                    {
                        payloadFetch.Type = typeof(Int32);
                        payloadFetch.Size = sizeof(Int32);
                        payloadFetch.Offset = offset;
                        break;
                    }
                case TypeCode.UInt32:
                    {
                        payloadFetch.Type = typeof(UInt32);
                        payloadFetch.Size = sizeof(UInt32);
                        payloadFetch.Offset = offset;
                        break;
                    }
                case TypeCode.Int64:
                    {
                        payloadFetch.Type = typeof(Int64);
                        payloadFetch.Size = sizeof(Int64);
                        payloadFetch.Offset = offset;
                        break;
                    }
                case TypeCode.UInt64:
                    {
                        payloadFetch.Type = typeof(UInt64);
                        payloadFetch.Size = sizeof(UInt64);
                        payloadFetch.Offset = offset;
                        break;
                    }
                case TypeCode.Single:
                    {
                        payloadFetch.Type = typeof(Single);
                        payloadFetch.Size = sizeof(Single);
                        payloadFetch.Offset = offset;
                        break;
                    }
                case TypeCode.Double:
                    {
                        payloadFetch.Type = typeof(Double);
                        payloadFetch.Size = sizeof(Double);
                        payloadFetch.Offset = offset;
                        break;
                    }
                case TypeCode.Decimal:
                    {
                        payloadFetch.Type = typeof(Decimal);
                        payloadFetch.Size = sizeof(Decimal);
                        payloadFetch.Offset = offset;
                        break;
                    }
                case TypeCode.DateTime:
                    {
                        payloadFetch.Type = typeof(DateTime);
                        payloadFetch.Size = 8;
                        payloadFetch.Offset = offset;
                        break;
                    }
                case GuidTypeCode:
                    {
                        payloadFetch.Type = typeof(Guid);
                        payloadFetch.Size = 16;
                        payloadFetch.Offset = offset;
                        break;
                    }
                case TypeCode.String:
                    {
                        payloadFetch.Type = typeof(String);
                        payloadFetch.Size = DynamicTraceEventData.NULL_TERMINATED;
                        payloadFetch.Offset = offset;
                        break;
                    }
                case TypeCode.Object:
                    {
                        // TypeCode.Object represents an embedded struct.

                        // Read the number of fields in the struct.  Each of these fields could be an embedded struct,
                        // but these embedded structs are still counted as single fields.  They will be expanded when they are handled.
                        int structFieldCount = reader.ReadInt32();
                        DynamicTraceEventData.PayloadFetchClassInfo embeddedStructClassInfo = ParseFields(ref reader, structFieldCount, fieldLayoutVersion);
                        if (embeddedStructClassInfo == null)
                        {
                            throw new FormatException($"Field {fieldName}: Unable to parse metadata for embedded struct");
                        }
                        payloadFetch = DynamicTraceEventData.PayloadFetch.StructPayloadFetch(offset, embeddedStructClassInfo);
                        break;
                    }

                case ArrayTypeCode:
                    {
                        if (fieldLayoutVersion == EventPipeFieldLayoutVersion.V1)
                        {
                            throw new FormatException($"EventPipeEventSource.ArrayTypeCode is not a valid type code in V1 field metadata.");
                        }

                        DynamicTraceEventData.PayloadFetch elementType = ParseType(ref reader, 0, fieldName, fieldLayoutVersion);
                        // This fetchSize marks the array as being prefixed with an unsigned 16 bit count of elements
                        ushort fetchSize = DynamicTraceEventData.COUNTED_SIZE + DynamicTraceEventData.ELEM_COUNT;
                        payloadFetch = DynamicTraceEventData.PayloadFetch.ArrayPayloadFetch(offset, elementType, fetchSize);
                        break;
                    }
                default:
                    {
                        throw new FormatException($"Field {fieldName}: Typecode {typeCode} is not supported.");
                    }
            }

            return payloadFetch;
        }

        // Guid is not part of TypeCode (yet), we decided to use 17 to represent it, as it's the "free slot"
        // see https://github.com/dotnet/coreclr/issues/16105#issuecomment-361749750 for more
        internal const TypeCode GuidTypeCode = (TypeCode)17;
        // Array isn't part of TypeCode either
        internal const TypeCode ArrayTypeCode = (TypeCode)19;

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
            
            if (Opcode == 0)
            {
                ExtractImpliedOpcode();
            }
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
        /// then we strip the "Start" or "Stop" from the event name and set the Opcode accordingly.
        /// </summary>
        private void ExtractImpliedOpcode()
        {
            if (EventName != null)
            {
                if (EventName.EndsWith("Start", StringComparison.OrdinalIgnoreCase))
                {
                    Opcode = (int)TraceEventOpcode.Start;
                    EventName = EventName.Substring(0, EventName.Length - 5);
                }
                else if (EventName.EndsWith("Stop", StringComparison.OrdinalIgnoreCase))
                {
                    Opcode = (int)TraceEventOpcode.Stop;
                    EventName = EventName.Substring(0, EventName.Length - 4);
                }
            }
        }

        private TraceEventNativeMethods.EVENT_RECORD* _eventRecord;
        private IntPtr _extendedDataBuffer;
    }
}
