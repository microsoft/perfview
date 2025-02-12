# NetTrace File Format v6

This spec outlines proposed changes between v5 and v6 of the format. Assuming we move forward this content should be
merged into the existing file format spec.

Recently we've been discussing creating some new profiling tools to improve the experience collecting profiling traces on Linux. Although the long-term plan is for the tools to support multiple formats, we identified nettrace as a good initial format to work with. The goal with the version 6 iteration of the format is to move nettrace towards being a little more general purpose so it works better with these tools. This includes adding multi-process support, richer event metadata, encoding a few data types more efficiently, and taking the opportunity to simplify some legacy aspects of the format which had needless complexity.

In terms of timeline I am expecting:
- Soon (Feb 2025)
  - Finalize the format and update the official file format spec in this repo
  - Release an updated version of the TraceEvent library that can read all current versions of NetTrace + the new version 6
- March 2025
  - Get the new version of TraceEvent into VS 
- Spring/Summer 2025
  - Create preview versions of tracing tools (naming TBD) which emit the new version 6 format
  - .NET runtime and dotnet-trace previews will *NOT* be using the updated v6 format during the .NET 10 release.
- End of 2025
  - Release the new tracing tools (naming TBD) that emit the version 6 format
- Future TBD
  - Update .NET runtime/dotnet-trace to use the version 6 format. 


## Simplify the StreamHeader, Object header/footers, alignment and versioning

### Motivation

NetTrace format up through v5 relies on an abstraction layer called FastSerialization that was originally designed as an alternative to binary serialization for managed .NET objects. It uses a versioning scheme in which every encoded object carries its own version number and arbitrary type. Although flexible, this is overly complicated here and the only way for a reader to confirm it understands all objects in the file is scan through and read the header for every object in the file. To avoid surprises the nettrace format always versioned the very first object in the stream, the Trace object, and then the set of allowed types and versions for everything else was inferable from that one. It works, but it just meant that all those other type names and version numbers on other objects are far more verbose than necessary.
The FastSerialization format also surrounds objects with both a variable sized header and a footer byte. The variable sized nature of the header means the reader either needs to do multiple reads to calculate the exact length or it needs to guess a read length that is likely to include the entire header and then have fallback logic to handle guessing wrong. The headers were also somewhat long making them inefficient if we ever wanted to add a large number of small objects to the file format in the future.
Last most of the objects in the nettrace format have a file alignment constraint for the content. This requires reader and writer to track the total number of bytes read/written to determine a variable sized padding field at the front of the object. As far as I know this has no real perf benefit as most of the content inside the objects is variable sized. If we do find padding useful in the content of some future object it would probably be better if the length of the padding was inferable from the padding byte values rather than forcing the reader to predict the length based on absolute stream offsets.

### Changes

#### New StreamHeader structure and version number

In v5 the file starts out with the 8 byte NetTrace Magic, followed by a 24 byte FastSerialization StreamHeader. In v6 the 8 byte NetTrace magic stays as is, followed by a new StreamHeader that is:

StreamHeader
{
  uint32 Reserved = 0
  uint32 MajorVersion = 6
  uint32 MinorVersion = 0
}

Like all the other integers in NetTrace format, these integers are encoded little endian. The initial 4 byte Reserved field overlaps where the first 4 bytes of the old FastSerialization StreamHeader. In prior versions that value was 20, the length of the "FastSerialization.1" string constant. In V6 onwards it is always zero. This aims to prevent any ambiguity determining which StreamHeader is present.
The intent of the MajorVersion and MinorVersion fields is that MajorVersion increments indicate breaking changes and MinorVersion increments indicate back-compatible changes.

After the stream header comes the Trace object (which is now renamed to Trace block)

#### Naming adjustment object -> block

The FastSerialization abstraction refered to all the top level items in the file after the stream header as 'objects' and then some types of individual objects were EventBlock, MetadataBlock, etc. Starting in V6 FastSerialization is gone so we are also getting rid of the 'object' terminology and just calling them all "blocks" going forward. This part is purely a descriptive change and has no impact on the format.

#### Updated block header

In V5 an object header consisted of:
- BeginPrivateObject Tag
- SERIALIZED TYPE
  - BeginPrivateObject Tag
  - NullReference Tag
  - version (int)
  - minimum reader version (int)
  - type name (string)
  - EndPrivateObject Tag

This header is 16 bytes + a variable length UTF8 string indicating the actual type of the object. The length of the object content was not included in the header. The EventBlock, MetadataBlock, and SPBlock objects included a BlockSize field as the first 4 bytes of the object content and the Trace object had fixed size.

In V6 the object header is replaced by a simplified block header:

```
struct BlockHeader
{
  uint24 BlockSize
  uint8 BlockKind
}
```

Readers may want to read the BlockHeader as a single little endian 4 byte integer X where BlockSize = X & 0xFFFFFF and BlockKind = X >> 24.


The type name strings used in the object header type name field map to new BlockKind values:

```
enum BlockKind
{
  EndOfStream = 0 // this is a new block type described later
  Trace = 1,
  Event = 2,
  Metadata = 3,
  Stack = 4,
  SequencePoint = 5
}
```

In V6 the EventBlock, MetadataBlock, StackBlock and SPBlock no longer have the initial BlockSize and padding fields. The value previously stored in the BlockSize object field is now in the BlockHeader. The semantics of the BlockSize header field are very similar to before, it represents the number of bytes in the remainder of the block. Specifically if file offset X points to the start of a block, then X + sizeof(BlockHeader) + BlockSize points to the start of the next block.

Readers should skip over any blocks with an unrecognized BlockKind. New blocks might be introduced in minor version updates to store new types of data and this is expected to be a back-compatible change because old readers skip over it.


#### Removed block footer

In V5 all objects ended with a trailing EndPrivateObject tag byte. In V6 this trailing byte serves no purpose and is removed.

#### Changed end of stream marker

In V5 the file ended with a NullReferenceTag byte. In V6 the file ends with a new EndOfStream block. This block consists only of a BlockHeader and has BlockSize=0.

## Multi-process support

### Motivation

Up to V5 the format was always designed to trace a single process only. Now we'd like to encode multi-process traces as well. This requires including process id information in places that previously only included thread ids. We also need to adjust definitions of a few TraceBlock fields.

### Changes

#### New ThreadBlock

A new top-level block, BlockHeader.Kind=6, contains a set of thread id/process id tuples referenced by index. This allows many events to refer the same (thread id, process id) without needing to repeatedly encode the same pair of potentially large IDs.

The content of a thread block is:

- Count - uint16
- Concatenated sequence of Count entries, each of which is
  - Index - varuint
  - ThreadId - varuint
  - ProcessId - varuint

When referencing a ThreadBlock entry by index, the reference must occur no later in the stream than the content of the next SequencePoint block and no earlier than ThreadBlock itself. 

#### Use ThreadIndex in EventBlob headers and SequencePoint blocks

In V5 the EventBlob has two header fields, ThreadId and CaptureThreadId which contained thread ids. In V6 these fields now contain indexes into the ThreadBlock table. The fields are renamed ThreadIndex and CaptureThreadIndex for clarity. Also the compressed header format previously used a flag bit (Flags & 0x4) to indicate if the ThreadId field was different than in the previous entry. Starting in V6 (Flag & 4) is not set, that means ThreadIndex is the same as CaptureThreadIndex. This should result in the bit not being set as frequently and header sizes on average will be a little smaller.

The SequencePoint block in V5 has a list of (ThreadId,SequenceNumber) tuples. All the ThreadIds in that list are now replaced with indexes into the ThreadBlock table and the field is renamed ThreadIndex.

#### Updated semantics for Trace fields

The TraceBlock PointerSize field represents the machine pointer size for traces that may contain more than one process. For processes whose bitness is smaller than the machine bitness (such as when using an emulator), memory addresses should be zero extended to the machine pointer size.

## New TraceBlock Metadata

### Motivation

Its useful for trace authors to include some descriptive information about the trace such as a machine name, environment name, or sampling rates. Rather than try to anticipate every piece of data that might be useful we wanted to add a simple open-ended mechanism to append key-value data to a trace.

### Changes

#### Updated TraceBlock

In V5 the Trace Object is defined:

- SyncTimeUTC:
 - Year - short
 - Month - short
 - DayOfWeek - short
 - Day - short
 - Hour - short
 - Minute - short
 - Second - short
 - Millisecond - short
- SyncTimeQPC - long
- QPCFrequency - long
- PointerSize - int
- ProcessId - int
- NumberOfProcessors - int
- ExpectedCPUSamplingRate - int

In V6 the format is:

- SyncTimeUTC:
 - Year - short
 - Month - short
 - DayOfWeek - short
 - Day - short
 - Hour - short
 - Minute - short
 - Second - short
 - Millisecond - short
- SyncTimeQPC - long
- QPCFrequency - long
- PointerSize - int
- KeyValueCount - int
- A sequence of KeyValueCount entries, each of which is:
  - key - string      // varuint prefixed UTF8 string
  - value - string    // varuint prefixed UTF8 string

For the three fields that were removed, they can be optionally encoded as key/value pairs where the key names are:

- NumberOfProcessors -> "HardwareThreadCount"
- ProcessId -> "ProcessId"
- ExpectedCPUSamplngRate -> "ExpectedCPUSamplingRate"

## Updated type metadata and additional payload field types

### Motivation

For file format size efficiency and writing efficiency there are a handful of new event payload paramater encodings we'd like to support. Each new encoding requires a corresponding representation in type metadata so that the file format readers understand how to decode it. We also want to support optional message templates and a human readable event description so that tools can better describe events to engineers viewing the traces.
Last, we are taking the opportunity to simplify the metadata encoding format. Currently there is a variety of unused, redundant, and inefficient data in the V5 metadata encoding.

### Changes

#### Updated encoding for MetadataBlock

In V5 the MetadataBlock reused the format of the EventBlock but many of the fields were unused. V6 uses an updated streamlined format:

- uint16 Count
- Concatenated sequence of Count MetadataBlobs, each of which is:
  - varuint MetaDataId;       // The Meta-Data ID that is being defined.
  - string ProviderName;      // varuint length-prefixed UTF8 string (no null terminator)
  - varuint EventId;          // A small number that uniquely represents this Event within this provider.  
  - string EventName;         // varuint length-prefixed length prefixed UTF8 string (no null terminator)
  - FieldDescriptions;        // a list of the payload field names and types - described below
  - OptionalMetadata;         // a list of optional metadata properties that can be attached to an event - described below

FieldDescriptions format is:

- varuint Count
- Contenated sequence of Count field descriptions, each of which is:
  - Type;                     // The type of a field - described below
  - string FieldName;         // varuint length-prefixed UTF8 string (no null terminator)

Type format is:

- uint8 TypeCode;             // Type is a discriminated union and this is the discriminant. 
- optional Type ElementType;  // Only present if TypeCode == Array (19).
- optional FieldDescriptions ObjectFields; // Only present if TypeCode == Object (1).

```
enum TypeCode
{
  Object = 1,                        // Concatenate together all of the encoded fields
  Boolean = 3,                       // A 4-byte LE integer with value 0=false and 1=true.  
  UTF16Char = 4,                     // a 2-byte UTF16 encoded character
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
  VarInt = 20,                       // New in V6: variable-length signed integer with zig-zag encoding (defined the same as in Protobuf)
  VarUInt = 21,                      // New in V6: variable-length unsigned integer (defined the same as in Protobuf)
  LengthPrefixedUTF16String = 22,    // New in V6: A string encoded with UTF16 characters and a UInt16 element count prefix. No null-terminator.
  LengthPrefixedUTF8String = 23,     // New in V6: A string encoded with UTF8 characters and a Uint16 length prefix. No null-terminator.
  VarLengthPrefixedUtf8String = 24,  // New in V6: A string encoded with UTF8 characters and a Varuint length prefix. No null-terminator.
}
```

OptionalMetadata format is:

- varuint Count 
- Concatenated sequence of Count optional metadata elements, each of which is:
  - uint8 Kind;                      // Discriminates which kind of optional metadata will follow
  - if Kind==OpCode
    - uint8 OpCode
  - if Kind==KeywordLevelVersion
    - uint64 Keywords;               // 64 bit set of groups (keywords) that this event belongs to.
    - uint8 Level;                   // The verbosity (5 is verbose, 1 is only critical) for the event.
    - uint8 Version                  // The version number for this event.
  - if Kind==MessageTemplate
    - string MessageTemplate         // varuint length-prefixed UTF8 string (no null terminator)
  - if Kind==Description
    - string Description             // varuint length-prefixed UTF8 string (no null terminator)
  - if Kind==KeyValue
    - string Key                     // varuint length-prefixed UTF8 string (no null terminator)
    - string Value                   // varuint length-prefixed UTF8 string (no null terminator)
  - if Kind & LengthPrefixed != 0    // An extensibility mechanism so future versions of the file format can add new kinds of optional metadata
    - varuint Size                   // Encodes the size of the optional metadata element, not including Kind and Size fields
                                     // The reader should skip Size bytes of data after the Size field to get to the next optional metadata element

```
enum OptionalMetadataKind
{
  OpCode = 1,
  // 2 is no longer used. In format V5 it was V2Params
  KeywordLevelVersion = 3,
  MessageTemplate = 4,
  Description = 5,
  KeyValue = 6,
  LengthPrefixed = 127
}
```