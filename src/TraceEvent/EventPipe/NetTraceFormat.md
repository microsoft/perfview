# NetTrace File Format (Current Version 6)

NetTrace is a binary file format that stores a profiling or logging trace. Each trace primarily contains a sequence of self-describing strongly-typed timestamped events. Performance-wise the format
is designed to efficiently support streaming with balanced attention to event read/write throughput, file-size, memory usage, and latency. Optimized implementations should be able to
read/write millions of events per second with file sizes as low as a few bytes per event.

Originally the file format was designed to support the EventPipe tracing system in .NET Core. As of version 6 the format started getting usage outside of .NET specific scenarios. We've
tried to design it in a way that is fairly general purpose and most EventPipe specific details are optional. By convention files in this format use *.nettrace file extension.
For those interested in the changes between versions, there is a summary at the end of this document. There are also stand-alone docs describing prior versions of the format:
- [Versions 1-3](NetPerfFormat.md)
- [Versions 4 and 5](NetTraceFormat_v5.md).

## File format overview

Fundamentally, the format can be thought of as a small header followed by a sequence of serialized blocks of data. Each block has a type and a length that allows determining
what kind of data is stored in that block and how far to skip ahead in the file to find the next block. A file format writer is anticipated to write out blocks one at a time, appending them to the file as it goes.
There is a special block that marks the trace is complete at the end of the file.

Most blocks store row-oriented data, conceptually similar to a subset of rows in a database table. Blocks of the same type belong to the same table and have the same schema. Rows have IDs allowing
them to be referenced by rows in other tables. There are 5 different tables in the format:
- Event Table - Each row represents one event which occurs in the trace.
- Metadata Table - Each row represents a type of event that can occur in the trace. This stores information such as Event name, Event ID, and the set of fields that will be recorded for events of this type.
  Every event row references exactly one metadata row.
- Stack Table - Each row represents a stack trace that can be associated with an event. Stacks consist of a sequence of instruction pointers (IPs). Symbolic information that maps IPs to function names or
  source lines doesn't have a dedicated table in the format. Instead, it is either stored in external symbol files or stored within specially designated events in the trace.
- Thread Table - Each row represents a thread that typically has some information associated such as an OS thread id, OS process id, and/or name. Each event row refers to the thread that emitted it.
- Label List Table - Each row represents a set of labels that apply to events. Labels can be arbitrary key/value data but most commonly they are used to store correlation IDs that link events to some external
  request or unit of work that was going on when the event occurred. Each event row can reference one label list row.

In order to fully resolve all the data for one event, the reader needs to read an event row, then resolve the references to the metadata, stack, thread, and label list rows. When an event row references
a row in another table, the referenced row must have already appeared in a block earlier in the file. The reader just needs to cache the metadata, stack, thread, and label list rows as it encounters them
reading through the file sequentially. Also to control the size of the caches the reader maintains, the format includes some blocks that help the reader manage the cache. A 'SequencePoint' block indicates
that that the reader can safely discard all the stacks and label lists cached so far. Optional flags on the SequencePoint can be used to flush thread and metadata caches as well. There is also a 'RemoveThread' block
that indicates specific threads are no longer active and can be removed from the cache.

## Primitives and endianness

All primitives in format are stored in little-endian format. Descriptions of different data structures in format refer to various primitive types:

- `byte` or `unint8` - 8-bit unsigned integer
- `ushort` or `uint16` - 16-bit unsigned integer
- `short` or `int16` - 16-bit signed integer
- `int` or `int32` - 32-bit signed integer`
- `uint` or `uint32` - 32-bit unsigned integer
- `long` or `int64` - 64-bit signed integer
- `ulong` or `uint64` - 64-bit unsigned integer
- `Guid` - a 16 byte sequence
- `varuint` - a variable length unsigned integer. The least significant 7 bits of each byte are used to encode the number. The 8th bit is set to 1 if there are more bytes to read and 0 if it is the last byte.
- `varint` - a variable length signed integer. It can be decoded by reading it as a varuint and then converting it to a signed integer using the following formula: `result = (value >> 1) ^ (-(value & 1))`.
- `varuint64` - a varuint where the value must fit in a ulong after decoding.
- `varint64` - a varint where the value must fit in a long after decoding.
- `varuint32` - a varuint where the value must fit in a uint after decoding.
- `varint32` - a varint where the value must fit in an int after decoding.
- `byte[N]` - a byte sequence of fixed-length N
- string - A length-prefixed UTF8 encoded string. The length is encoded as a varuint32.

## First Bytes: NetTrace Magic

The beginning of the format is always the stream header.   This header's only purpose
is to quickly identify the format of this stream (file) as a whole and some versioning information.

It starts with the UTF8 encoding of the characters 'Nettrace', thus the first 8 bytes will be:

4E 65 74 74 72 61 63 65

Following that is the versioning information:

  uint32 Reserved = 0
  uint32 MajorVersion = 6
  uint32 MinorVersion = 0

Past versions of the format handled versioning information differently and did not use this layout. You can distinguish earlier versions either by the absence of the 'Nettrace' magic or because the Reserved field
is not zero. Its a little unfortunate but we hope not to change the header any further other than incrementing the version numbers.

Major version is intended to represent breaking changes in the format. A file format reader should not attempt to continue reading the file if the major version is higher than it understands. Minor version
is intended to represent non-breaking changes. A file format reader should continue reading the file regardless of minor version number.

## Blocks

The rest of the file is a sequence of blocks. Each starts with a BlockHeader:

```
struct BlockHeader
{
  uint24 BlockSize
  uint8 BlockKind
}
```

Readers can read the BlockHeader as a single little endian 4 byte integer X where BlockSize = X & 0xFFFFFF and BlockKind = X >> 24.

Following the block header there will be BlockSize bytes of data, then the BlockHeader for the next block. The format of this data depends on the BlockKind. The following block kinds are defined:

```
    enum BlockKind
    {
        EndOfStream = 0,
        Trace = 1,
        Event = 2,
        Metadata = 3,
        SequencePoint = 4,
        StackBlock = 5,
        Thread = 6,
        RemoveThread = 7,
        LabelList = 8
    }
```

Readers should skip over any blocks with an unrecognized BlockKind. New blocks might be introduced in minor version updates to store new types of data and this is expected to be a back-compatible change because old readers skip over it.


## TraceBlock

After the Stream Header, the first block in the file is the TraceBlock, which represents all the data about the Trace as a whole.   

This block is encoded as follows:

- SyncTimeUTC:
 - Year - short
 - Month - short
 - DayOfWeek - short
 - Day - short
 - Hour - short
 - Minute - short
 - Second - short
 - Millisecond - short
- SyncTimeTicks - long
- TickFrequency - long
- PointerSize - int
- KeyValueCount - int
- A sequence of KeyValueCount entries, each of which is:
  - key - string
  - value - string

SyncTimeUTC and SyncTimeQPC represent the time the trace was started. SyncTimeUTC is the time in UTC and SyncTimeTicks is the time in ticks. TickFrequency indicates the number of ticks in one second.
The remainder of the trace indicates all timestamps in units of ticks so these values allow converting those timestamps to UTC time.

PointerSize is the size of a pointer in bytes. There are some fields in the format that are encoded as pointer sized values.

The KeyValueCount field indicates the number of key-value pairs that follow. These are used to store arbitrary metadata about the trace. The key and value are both strings.
For compatibility with earlier versions of the format there are a few key names that are assigned special meaning. Using any of these keys is optional.

- "HardwareThreadCount" - In previous versions of the format this was the NumberOfProcessors Trace field. It represents the number of threads that can run concurrently on the hardware.
- "ProcessId" - In previous versions of the format this was the ProcessId Trace field. It represents the process id of the process that was being traced.
- "ExpectedCPUSamplingRate" - In previous versions of the format this was the ExpectedCPUSamplingRate Trace field. It was unused in the past but could be used to declare the frequency of CPU sampling events.

After the trace block any other kind of block could appear next. The reader is expected to continue reading blocks until it encounters an EndOfStream block.

## EventBlock

Each EventBlock contains a set of events and every event in the trace is contained within some EventBlock. The event block payload consists of the following concatenated fields:

- Header
  - HeaderSize - short - Size of the header including this field
  - Flags - short
  - Min Timestamp - long - The minimum timestamp of any event in this block
  - Max Timestamp - long - The maximum timestamp of any event in this block
  - optional reserved space based to reach HeaderSize bytes. Later versions of the format may embed extra data here so no assumption should be made about the contents.
- 1 or more Event rows described further below. The end of the last event should coincide exactly with the BlockSize for this block


## Event rows

In past versions of the format these were called 'Event blobs'.

An event row consists of a header and a payload. It can be encoded one of two ways, depending on the Flags field of the enclosing Block. If the flags field has the 1 bit set then events are in 'Header Compression' format, otherwise they are in uncompressed format. 

### Uncompressed events

In the uncompressed format an event row is:

- Header
  - EventSize - uint32 - The size of event blob not counting this field
  - MetadataId - uint32 - In the context of an EventBlock the low 31 bits are a foreign key to the event's metadata. In the context of a metadata block the low 31 bits are always zero'ed. The high bit is the IsSorted flag.
  - Sequence Number - uint32 - An incrementing counter that is used to detect dropped events
  - ThreadIndex - uint64 - An index into the thread table. It is usually the same thread that physically recorded the event, but in some cases thread A (CaptureThreadIndex) will record an event that represents state about thread B (ThreadIndex).
  - CaptureThreadIndex - uint64 - This is the thread which physically captured the event. Sequence Numbers are tracked relative to the CaptureThreadIndex.
  - ProcessorNumber - uint32 - Identifies which processor CaptureThreadIndex was running on.
  - StackId - uint32 - An index into the stack table. This is a stack for thread `ThreadIndex` that should be associated with this event.
  - TimeStamp - uint64 - The time in ticks the event occurred.
  - LabelListId - uint32 - An index into the label list table. This is a set of labels that apply to this event.
  - PayloadSize - uint32
- Payload - PayloadSize bytes of data. The format of this data is described by the field information in the metadata referenced by MetadataId.

### Header compression

In the header compression format an event row uses a few additional encoding techniques:

1. variable length encodings of int and long using the varunit primitive.

2. Optional fields - The first byte of the event header is a set of flag bits that are used to indicate whether groups of fields are present or not. For each group of fields that is not present there is calculation that can be done based on the previously decoded event header in the same block to determine the field values.

3. Delta encoding - In cases where the field is present, the value that is encoded may be a relative value based on the same field in the previous event header. When starting a new event block assume that the previous event contained every field with a zeroed value.


Compressed events are encoded with a header:

- Flags byte
- MetadataId optional varuint32 
  - if Flags & 1 the value is read from the stream
  - otherwise use the previous MetadataId
- SequenceNumber optional varuint32 
 - if Flags & 2 the value is read from the stream + previous SequenceNumber
 - otherwise previous sequence number
 - in either case increment SequenceNumber by 1
- CaptureThreadIndex optional varuint64
  - if Flags & 2 the value is read from the stream
  - otherwise previous CaptureThreadIndex
- Processor Number optional varuint32
  - if Flags & 2 the value is read from the stream
  - otherwise previous Processor Number
- ThreadIndex optional varuint64
  - if Flags & 4 the value is read from the stream
  - otherwise use the previous ThreadIndex
- StackId optional varuint32
  - if Flags & 8 the value is read from the stream
  - otherwise previous StackId
- TimeStamp varuint64 - read value from stream + previous value
- LabelListId optional varunit32
  - if Flags & 16, read from stream
  - otherwise previous LabelListId
- IsSorted has value (flags & 64) == 64
- PayloadSize optional varuint32
  - if Flags & 128, read from stream
  - otherwise previous PayloadSize

The compressed header is followed by PayloadSize bytes of event payload and no alignment.

### Event Sequence Numbers

Every thread that can log events assigns each event a sequence number. Each thread has an independent counter per-session which starts at 1 for the first event, increments by 1 for each successive event, and if necessary rolls over back to zero after event 2^32-1. Sequence numbers are assigned at the time the event generating code requests to log an event, regardless of whether the events are committed into the file stream or dropped for whatever reason. This allows the eventual reader of the file format to locate gaps in the sequence to infer that events have been lost. Spotting these gaps can occur two ways:

1. For a given CaptureThreadIndex the sequence number should always increase by 1. If events on the same CaptureThreadIndex skip sequence numbers then events have been dropped.
2. Every SequencePointBlock (described later in this doc) contains a lower bound for the sequence number on each thread. If the sequence point records a higher sequence number for a given thread then the last observed event on that thread, events have been dropped. This allows detecting cases where a thread is dropping all of its events, likely because all buffers are remaining persistently full.

### Event TimeStamp sorting

In order to lower overhead of emitting events and to improve locality for events emitted by the same thread, the writer is not required to fully sort events based on time when emitting them. Instead if the reader wants to produce a strongly time-ordered event sequence then it will need to do some sorting. The file format does offer more limited guarantees to assist the reader in doing this sort efficiently while streaming.

1. All events with the same CaptureThreadIndex are guaranteed to be sorted by TimeStamp relative to each other. Only events with differing CaptureThreadIndex may have timestamps that are out of order in the file. For example the file might first have a run of events on thread A with timestamps 10, 15, 20, then a run of events on thread B with timestamps 12, 13, 14.

2. All events in between a pair of sequence points in file order are guaranteed to have timestamps in between the sequence point times. This means sorting each set of events between sequence points and then concatenating the lists together in file order will generate a complete sort of all events. The writer should bound the number of events between sequence points so sorting the file is ultimately O(N) for an N byte file as sorting each chunk is O(1) and there are N chunks.

3. In order to produce on-demand sorts with low latency in streaming situations, some events are marked with the IsSorted flag set to true. This flag serves as a weaker version of the sequence point and indicates that every event after this one in file order has a timestamp which is >= to the current event. For streaming this means the reader can safely sort and emit all events it has cached in the current sequence point region that have a timestamp older than this event. It is guaranteed no future event will have an earlier timestamp which would invalidate the sort. The runtime writes batches of events that have queued up at small time intervals (currently 100ms, maybe configurable in the future) and it is guaranteed at least one event in every batch will have this flag set. As long as the file writer is able to keep up with the rate of event generation this means every event in the previous batch will have a timestamp less than the marked event in the next batch and all of them can be sorted and emitted with a one time unit latency. 


## MetadataBlock

Each MetadataBlock holds a set of metadata rows. Each metadata row has an ID and it describes one type of event. Each event has a metadataId field which will indicate the ID of the metadata row which describes that event. Metadata includes an event name, provider name, and the layout of fields that are encoded in the event's payload section.

The metdata block payload is encoded as follows:

- uint16 HeaderSize           // The size of the header, not including this HeaderSize field
  - Undefined header          // An optional sequence of HeaderSize bytes that are not interpreted by the reader. This is useful for future extensibility.
- Concatenated sequence of metadata rows, each of which is:
  - uint16 Size;              // The size of the metadata row in bytes not including the Size field 
  - varuint32 MetaDataId;     // The metdata row ID that is being defined.
  - string ProviderName;      // The name of a provider which serves as a namespace for the event.
  - varuint32 EventId;        // A small number that uniquely represents this Event within this provider or zero if no IDs is available.
  - string EventName;         // A name for this type of event
  - FieldDescriptions;        // a list of the payload field names and types - described below
  - OptionalMetadata;         // a list of optional metadata properties that can be attached to an event - described below
  - Undefined;                // An optional sequence of bytes that are not interpreted by the reader. This is useful for future extensibility. Size is inferred from the Size field.

FieldDescriptions format is:

- uint16 Count
- Contenated sequence of Count field descriptions, each of which is:
  - uint16 FieldSize;         // The size of the field description in bytes not including the FieldSize field
  - string FieldName;         // The name of the field
  - Type;                     // The type of a field - described below
  - Undefined;                // An optional sequence of bytes that are not interpreted by the reader. This is useful for future extensibility. Size is inferred from the FieldSize field.

Type format is:

- uint8 TypeCode;                          // Type is a discriminated union and this is the discriminant. 
- optional Type ElementType;               // Only present if TypeCode == Array (19), FixedLengthArray(22), RelLoc(24) OR DataLoc(25)
- optional uint16 ElementCount;            // Only present if TypeCode == FixedLengthArray(22).
- optional FieldDescriptions ObjectFields; // Only present if TypeCode == Object (1).

```
enum TypeCode
{
  Object = 1,                        // Concatenate together all of the encoded fields
  Boolean32 = 3,                     // A 4-byte LE integer with value 0=false and 1=true.  
  UTF16CodeUnit = 4,                 // a 2-byte UTF16 code unit (Often this is a character, but some characters need more than one code unit to encode)
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
  DateTime = 16,                     // Encoded as 8 concatenated Int16s representing year, month, dayOfWeek, day, hour, minute, second, and milliseconds.
  Guid = 17,                         // A 16-byte guid encoded as the concatenation of an Int32, 2 Int16s, and 8 Uint8s
  NullTerminatedUTF16String = 18,    // A string encoded with UTF16 characters and a 2-byte null terminator
  Array = 19,                        // a UInt16 length-prefixed variable-sized array. Elements are encoded depending on the ElementType.
  VarInt = 20,                       // New in V6: variable-length signed integer with varint64 encoding.
  VarUInt = 21,                      // New in V6: variable-length unsigned integer with varuint64 encoding.
  FixedLengthArray = 22,             // New in V6: A fixed-length array of elements. The length is determined by the metadata.
                                     // Format: Concatenate together ElementCount number of ElementType elements.
  UTF8CodeUnit = 23,                 // New in V6: A 1-byte UTF8 code unit.  (Often this is a character, but some characters need more than one code unit to encode)
  RelLoc = 24,                       // New in V6: An array at a relative location within the payload. 
                                     // Format: 4 bytes where the high 16 bits are size and low 16 bits are position relative to after this field.
                                     // Size is measured in bytes, not elements. The element type must be fixed sized.
  DataLoc = 25,                      // New in V6: An absolute data location within the payload.
                                     // Format: 4 bytes where the high 16 bits are size and low 16 bits are position relative to start of the event parameters buffer.
                                     // Size is measured in bytes, not elements. The element type must be fixed sized.
  Boolean8 = 26                      // New in V6: A 1-byte boolean where 0=false and 1=true.
}
```

The RelLoc and DataLoc types only support fixed size element types. To determine if a type is fixed size:
- Object is fixed size iff all of the field types are fixed size.
- FixedLengthArray is fixed size iff the element type is fixed size.
- Array, NullTerminatedUTF16String, and LengthPrefixedUTF(8|16)String are not fixed size.
- All other types are fixed size.

OptionalMetadata format is:

- uint16 Size 
- Concatenated sequence of optional metadata elements using Size bytes in total, each of which is:
  - uint8 Kind;                      // Discriminates which kind of optional metadata will follow
  - if Kind==OpCode
    - uint8 OpCode
  - if Kind==Keyword
    - uint64 Keywords;               // 64 bit set of groups (keywords) that this event belongs to.
  - if Kind==MessageTemplate
    - string MessageTemplate
  - if Kind==Description
    - string Description
  - if Kind==KeyValue
    - string Key
    - string Value
  - if Kind==ProviderGuid
    - Guid ProviderGuid
  - if Kind==Level
    - uint8 Level                    // The verbosity (5 is verbose, 1 is critical) for the event.
  - if Kind==Version
    - uint8 Version                  // The version number for this event.

```
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
```


## StackBlock Object

The stack block contains a set of stacks that can be reference from events using an ID. This allows many events to refer the same stack without needing to repeat its encoding for every event.

The payload of a stack block is:

- Header
  - First Id - uint32
  - Count - uint32
- Concatenated sequence of Count stacks, each of which is
  - StackSize in bytes - uint32
  - StackSize number of bytes

On 32 bit traces the stack bytes can be interpreted as an array of 4 byte integer IPs. On 64 bit traces it is an array of 8 byte integer IPs. Each stack can be given an ID by starting with First Id and incrementing by 1 for each additional stack in the block in stream order. 

## SequencePointBlock

A sequence point is used as a stream checkpoint and serves several purposes.

1. It demarcates time with a TimeStamp. All the events in the stream which occurred prior to this TimeStamp are located before this sequence point in file order. Likewise all events which happened after this timestamp are located afterwards. The TimeStamps on successive Sequence points always increase so if you are interested in an event that happened at a particular point in time you can search the stream to find SequencePoints that occurred beforehand/afterwards and be assured the event will be in that region of the file.

2. It contains a list of threads with lower bounds on the last event sequence number that thread attempted to log. This can be used to detect dropped events. There is no requirement for all (or any) threads to be in this list. A writer might omit them if it doesn't care about detecting dropped events or it knows there are no dropped events to detect. See the section on Sequence numbering for more info.

3. The sequence point serves as a barrier for stack references, label list references, and optionally thread references. Events are only allowed to make references if there is no sequence point in between the EventBlock and the StackBlock/LabelListBlock/ThreadBlock in file order. This simplifies cache management by ensuring only a bounded region of blocks need to be kept in memory to resolve references.

A SequencePointBlock payload is encoded:

- TimeStamp uint64
- Flags - uint32
- ThreadCount - uint32
- A sequence of ThreadCount threads, each of which is encoded:
  - ThreadIndex - varuint64
  - SequenceNumber - varuint32

If Flags & 1 == 1 then the sequence point also flushes the thread cache. Logically the flush occurs after doing any thread sequence number checks so the reader can still detect dropped events.
If Flags & 2 == 2 then the sequence point also flushes the metadata cache.

## EndOfStreamBlock

The stream ends with an EndOfStreamBlock. This block has no payload and is only used to indicate that the stream has ended. The reader should stop reading blocks after encountering this block. A future version of the format may add
some data to this block but currently it has zero size.

## ThreadBlock

ThreadBlock, BlockHeader.Kind=6, contains descriptive information about threads in the trace. This allows many events to refer the same thread without needing to repeatedly encode large IDs or names. Although threads were originally
designed to represent OS threads, they can be used for any logical flow of execution that emits events such as a fiber, a task, a green thread, a hardware processor core, etc.

The content of an Thread block is:

- Concatenated sequence of rows, each of which is:
  - RowSize - uint16    // The size of the row in bytes not including the RowSize field
  - Index - varuint64   // The index used to refer to this thread from other blocks
  - zero or more OptionalThreadInfo entries each of which is:
    - Kind - uint8
    - if(Kind == Name (1))
      - Name - string
    - if(Kind == OSProcessId (2))
      - Id - varuint64
    - if(Kind == OSThreadId (3))
      - Id - varuint64
    - if(Kind == KeyValue (4))
      - Key - string
      - Value - string

It is valid to reference a thread index any time in file order after the Thread block which introduces it and prior to the RemoveThread block that removes it.

Ideally an OS thread would have exactly one thread index for its lifetime but it is allowable to give the same OS thread multiple indices if the event writer is unable to track the thread's identity. If there is more than one index mapped to the same OS thread ID then the reader may treat this as the OS recycling the same ID across multiple threads or that the OS isn't guaranteeing unique IDs.

The Thread block doesn't have timestamps and is not intended to indicate when a given thread is created. Its possible for the thread to exist before the trace provides an index for it, or it is technically legal for the trace to define indexes for threads that have not yet been created. If the trace wants to define the time a given thread was created or destroyed that is best done with some agreed upon event inserted into the event stream.


## RemoveThreadBlock

RemoveThreadBlock, BlockHeader.Kind=7, contains a set of (threadIndex, sequenceNumber) tuples. The RemoveThreadBlock explicitly removes thread indexes from the active set of threads so a parser no longer needs to track them. It also provides the
final sequenceNumber for events emitted by each thread to determine if any events were dropped from the stream.

The content of a RemoveThread block is:

- Concatenated sequence of entries, each of which is:
  - Index - varuint
  - SequenceNumber - varuint

The RemoveThread block doesn't have timestamps and is not intended to indicate when a given thread is destroyed. Its possible for the thread to continue existing after the trace indicates it is removed. If the trace wants to define the time a given thread was destroyed that is best done with some agreed upon event inserted into the event stream.

## LabelListBlock

The LabelList block, BlockHeader.Kind=8, contains a set of key-value pairs that can be referenced by index within the EventHeader. This allows many events to refer to the same key-value pairs without needing to repeatedly encode them.

The content of a LabelListBlock is:

- firstIndex - uint32  // The index of the first entry in the block. Each successive entry is implicitly indexed by the previous entry's index + 1. firstIndex must be >= 1.
- count - uint32       // The number of entries in the block.
- Concatenated sequence of label_lists, each of which is:
  - one or more Label entries each of which is:
    - Kind - uint8
    - if(Kind & 0x7F == 1)
      - ActivityId - Guid
    - if(Kind & 0x7F == 2)
      - RelatedActivityId - Guid
    - if(Kind & 0x7F == 3)
      - TraceId - byte[16]
    - if(Kind & 0x7F == 4)
      - SpanId - uint64
    - if(Kind & 0x7F == 5)
      - Key - string
      - Value - string
    - if(Kind & 0x7F == 6)
      - Key - string
      - Value - varint64
    - if(Kind & 0x7F == 7)
      - OpCode - uint8
    - if(Kind & 0x7F == 8)
      - Keywords - uint64
    - if(Kind & 0x7F == 9)
      - Level - uint8
    - if(Kind & 0x7F == 10)
      - Version - uint8

If the high bit of the Kind field is set that demarcates that this is the last label in a label list and the next label is part of the next list.

Similar to StackBlock, references to a row in the LabelListBlock are only valid in the file after the LabelListBlock that defines it and before the next SequencePoint block.
This prevents the reader from needing a lookup table that grows indefinitely with file length or requiring the reader to search the entire file to resolve a given label list index.

An empty LabelList can't be explicitly encoded in the LabelListBlock, but implicitly the LabelList index 0 refers to an empty LabelList.

The OpCode, Keywords, Level, and Version fields are considered to override any value found in the metadata for the event. The trace writer is free to provide these values in either metadata
or in a LabelList, but for the common scenario where they are the same across all events of a given type metadata is probably the more space efficient option.

## Changes relative to older file format versions

### Versions 1-3

Versions 1-3 were only used internally during a development milestone and were never released to the public. They were not intended to be a stable format and were not documented.

### Version 3 (NetPerf) -> 4 (NetTrace)

**Breaking change**

In general the file format became a little less simplistic in this revision to improve size on disk, speed up any IO bound read/write scenario, detect dropped events, and efficiently support a broader variety of access patterns. In exchange implementing a parser became a bit more complex and any read scenario that was previously memory or CPU bound likely degraded.
Although not intending to abandon the original goals of simplicity, this definitely takes a few steps away from it in the name of performance. One of the original premises was that the format should make no effort to at conserving size because generic compression algorithms could always recover it. This still feels true in the case where you have a file on disk and there is no tight constraint on CPU or latency to compress it. This doesn't appear to hold up as well when you assume that you want to do streaming with low latency guarantees and constrained writer CPU resources. General purpose compression algorithms need both CPU cycles and sufficient blocks of data to recognize and exploit compressible patterns. On the other hand we have a priori knowledge about potentially large parts of the format that are highly compressible (often better than 10:1). At the cost of some complexity we can write targeted algorithms that recover considerable low hanging fruit at very low CPU/latency cost.

Changes:

1. The 'Nettrace' magic was added to the front of the file to better distinguish Nettrace from any other serialized output of the FastSerializer library.
2. Metadata was moved out of EventBlocks and into separate MetadataBlocks. This makes it dramatically easier to locate in scenarios where the reader is searching only for specific events and the metadata necessary to interpret those events rather than wanting to parse every event in the stream.
3. Stacks are interned and stored in StackBlocks rather than inlined into each event. This gives considerable file size savings in scenarios where stacks are commonly repeated. This added StackBlocks and the StackId event field.
4. Header compression often dramatically reduces the file size overhead. Previously we paid a fixed 56 bytes per event. Anecdotally in a scenario I looked at recently compressed headers average 5 bytes despite encoding more data than before.
5. Timestamp ranges in the headers of EventBlocks help locate EventBlocks of interest without having to parse every event inside them.
6. Sequence numbering events aids in dropped event detection. This added the SequenceNumber and CaptureThreadId fields to event blobs.
7. ThreadId is now 64 bit instead of 32 bit to support large thread IDs used by some OSes.
8. Events are no longer guaranteed to be sorted. This alleviates potential cost and scalability concern from the runtime emitting the events in favor of paying that cost in the reader where processing overhead is more easily accepted. The IsSorted field was added to events to help the reader quickly sort them.
9. SequencePoint blocks are new and were added to support several of the above features: Cache boundaries for the stack interning, dropped event detection for the seqeunce numbering, and cache management/sort boundaries for the unsorted events.
10. The version number of the Trace object moved from 3 -> 4 and the version of EventBlock moved 1 -> 2.
11. BeginObject tags are replaced by PrivateBeginObject to avoid memoization based memory leaks in the FastSerialization library.

### Version 4 -> 5

**Non-breaking change**

This version adds a set of optional tags after the metadata header and payload. These tags are used to describe the opcode of an event or include metadata for types that are unsupported in V4 and earlier.

We expect that readers that don't understand these tags will simply ignore them and continue to parse the rest of the metadata as they would have in V4.

### Version 5 -> 6

**Breaking change**

This version was motivated by using the format outside of existing .NET scenarios and wanting to make it more general purpose. Changes:

#### Simplify the StreamHeader, Object header/footers, alignment and versioning

NetTrace format up through v5 relies on an abstraction layer called FastSerialization that was originally designed as an alternative to binary serialization for managed .NET objects. It uses a versioning scheme in which every encoded object carries its own version number and arbitrary type. Although flexible, this is overly complicated here and the only way for a reader to confirm it understands all objects in the file is scan through and read the header for every object in the file. To avoid surprises the nettrace format always versioned the very first object in the stream, the Trace object, and then the set of allowed types and versions for everything else was inferable from that one. It works, but it just meant that all those other type names and version numbers on other objects are far more verbose than necessary.
The FastSerialization format also surrounds objects with both a variable sized header and a footer byte. The variable sized nature of the header means the reader either needs to do multiple reads to calculate the exact length or it needs to guess a read length that is likely to include the entire header and then have fallback logic to handle guessing wrong. The headers were also somewhat long making them inefficient if we ever wanted to add a large number of small objects to the file format in the future.
Last most of the objects in the nettrace format have a file alignment constraint for the content. This requires reader and writer to track the total number of bytes read/written to determine a variable sized padding field at the front of the object. As far as I know this has no real perf benefit as most of the content inside the objects is variable sized. If we do find padding useful in the content of some future object it would probably be better if the length of the padding was inferable from the padding byte values rather than forcing the reader to predict the length based on absolute stream offsets.

1. Created a new simplified StreamHeader and removed the FastSerialization header
2. Removed the FastSerialization object header and footer from all objects, replacing them with a simplified BlockHeader
3. Removed the FastSerialization end of stream tag and replaced it with the EndOfStream block.

#### Multi-process support

Up to V5 the format was always designed to trace a single process only. Now we'd like to encode multi-process traces as well. This requires including process id information in places that previously only included thread ids. We also need to adjust definitions of a few TraceBlock fields.

1. Added new blocks ThreadBlock and RemoveThreadBlock
2. Use ThreadIndex (a reference into the ThreadBlock) in EventBlob headers and SequencePoint blocks instead of ThreadIds
3. The SequencePointBlock now includes a flags field to indicate whether the reader's thread cache should also be reset and the thread sequence point list was shifted to variable size integer encoding.
4. The TraceBlock PointerSize field now represents the machine pointer size for traces that may contain more than one process. All stack block IPs should use this pointer size even if they need to be zero-extended.

#### New TraceBlock Metadata

Its useful for trace authors to include some descriptive information about the trace such as a machine name, environment name, or sampling rates. Rather than try to anticipate every piece of data that might be useful we wanted to add a simple open-ended mechanism to append key-value data to a trace.

1. Added the KeyValuePair mechanism to the trace block to support arbitrary metadata.
2. Removed the NumberOfProcessors, ProcessId, and ExpectedCPUSamplingRate fields from the TraceBlock and replaced them with optional key-value pairs.

#### Updated type metadata and additional payload field types

For file format size efficiency and writing efficiency there are a handful of new event payload field encodings we'd like to support. Each new encoding requires a corresponding representation in type metadata so that the file format readers understand how to decode it. We also want to support optional message templates and a human readable event description so that tools can better describe events to engineers viewing the traces.
Last, we are taking the opportunity to simplify the metadata encoding format. There was a variety of unused, redundant, and inefficient data in the V5 metadata encoding.

1. Metadata rows are no longer encoded with EventHeaders.
2. Most of the metadata fields are now optional and the top-level format of a metadata row was redesigned.
3. The 2nd copy of field information that was added by V2Params in version 5 has been removed. It only existed to support adding array support in a non-breaking way and now arrays are supported in the same FieldDescriptions as all the other types.
4. New payload field types were added: VarInt, VarUInt, FixedLengthArray, UTF8CodeUnit, RelLoc, DataLoc, and Boolean8. The existing Boolean type was renamed to Boolean32 to avoid ambiguity. 
5. Strings in the metadata are now UTF8 rather than UTF16

#### Extended support for event labels

In V5 every event had a fixed set of fields that could be encoded in the header + the payload area fields were specific to the event type.
There was no way to add new optional fields that are independent of the event type such as new correlation identifiers or other types of ambient enrichment data.
In particular OpenTelemetry has embraced the W3C TraceContext standard for distributed tracing and I'd like to be able to use that as a common correlation identifier in the future.

1. Adds a new LabelListBlock to store key-value pairs that can be referenced by index within the EventHeader.
2. Removes the ActivityId and RelatedActivityId fields from the EventHeader and replaces them with a LabelListIndex field. Activity ids can be stored within the LabelList.

#### Support readers/writers operating with limited memory

In V5 metadata ids were global across the entire trace which implies that readers and writers should maintain a dictionary that grows with the number of event types. Technically the writers could forget the ID assignments for older events and re-emit the same metadata under a new ID later but that makes the memory requirements even worse for readers who now have multiple IDs for the same metadata. In V6 the SequencePointBlock includes a flag that controls flushing the metadata cache so that writers can communicate to readers when it is safe to forget old IDs. 
