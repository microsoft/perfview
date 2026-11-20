# Universal Providers Tracing Events

## Overview
This document describes the definitions of a set of universal machine-wide tracing events that are consumed by TraceEvent and exposed in PerfView.

## Format Restrictions
 - Any format that can be converted to a TraceLog (ETLX) by TraceEvent can use these event definitions.  At this time, PerfView will only consume them from nettrace files.
 - Formats must provide a common set of fields with each event.  These fields are used by in addition to the payloads listed in this document to interpret the events.  The fields do not explicitly require data types.  If they don't match what TraceEvent uses for storage, then TraceEvent will need to convert them.  It is recommended to use the types specified in the field descriptions below (if specified).
 - Formats must provide the QPC frequency to ensure that times that are stored in the Value fields of the Universal Events Provider can be interpreted.

### Required Fields
 - TimeStamp: The time that the event occurred.
 - Process ID: The integer identifier of the process that caused the event to be written.
 - Thread ID: The integer identifier of the thread that caused the event to be written.
 - Processor ID: The integer identifier of the processor associated with the event.

## Common Format Information
 - All integers are of type varint encoded in ULEB64/7-bit encoded format.
 - All strings are length-prefixed (16-bit unsigned integer) followed by UTF8 bytes.  Not null-terminated.

## Universal System Provider
The universal system provider is named "Universal.System". It's purpose is to provide system-level events for reconstructing the state of a machine and its processes during a period of time.

### Event Definitions
- **Event Name**: ProcessCreate
    - **Description**: Identifies a newly created processes. The process ID is inferred from the format's process ID field.
    - **Field: NamespaceId**: varint - The actual process ID on the system within the running namespace.
    - **Field: Name**: string - The name of the process.
    - **Field: NamespaceName**: string - Namespace of the process. Can be used to convey a set of related processes.

- **Event Name**: ProcessExit
    - **Description**: Identifies a process that has exited. The process ID is inferred from the format's process ID field.
    - **Stable Event ID**: 2
    - Ends all mappings belonging to this process instance after samples at the exit timestamp.

- **Event Name**: ProcessMapping
    - **Stable Event ID**: 3
    - **Description**: Identifies a mapping (file, memory, etc.) associated with a specific process.
    - **Field: Id**: varint - A unique id within this process instance. May also occur in another process, for example after fork. Used to identify this mapping in other events.
    - **Field: StartAddress**: varint - Starting virtual address of the mapping within the process.
    - **Field: EndAddress**: varint - Inclusive ending virtual address of the mapping within the process.
    - **Field: FileOffset**: varint - Offset of the actual file on disk that StartAddress correlates to.  For PE files this is always zero.
    - **Field: FileName**: string - Name of the file the mapping represents.  Empty if none.
    - **Field: MetadataId**: varint - Identifier for a matcing ProcessMappingMetadataEvent.  0 means no metadata exists for this mapping.

- **Event Name**: ProcessMappingUnload
    - **Stable Event ID**: 6
    - **Description**: Removes the intersection of the specified inclusive range and the live mapping identified by Id in this process instance. The process ID and timestamp come from the event header.
    - **Field: Id**: varint - Id of the ProcessMapping to unload.
    - **Field: StartAddress**: varint - First virtual address to unload.
    - **Field: EndAddress**: varint - Inclusive last virtual address to unload.
    - Surviving pieces retain the same Id, and their ProcessSymbol events continue to refer to that Id via MappingId. An unknown Id or a range outside the mapping has no effect.

- **Event Name**: ProcessMappingMetadata
    - **Description**: Contains symbol and version information for the mapping if available.
    - **Field: Id**: varint - Trace-wide unique identifier for the mapping metadata. Often seen in multiple ProcessMapping events when the same executable file is loaded into multiple processes.
    - **Field: SymbolMetadata**: string - Symbol metadata in JSON.  Describes the type (ELF/PE) and details that enable looking up symbols locally or remotely.
    - **Field: VersionMetadata**: string - Version metadata in JSON.  Describes the build and version details.

- **Event Name**: ProcessSymbol
    - **Description**: Maps a range of address space to a readable name (symbol).
    - **Field: Id**: varint - Trace-wide unique identifier for the symbol.
    - **Field: MappingId**: varint - Id of the ProcessMapping in the event header's process instance that contains the symbol. Emit symbols separately for each process that uses a shared mapping Id.
    - **Field: StartAddress**: varint - Starting virtual address of the symbol within the mapping.
    - **Field: EndAddress**: varint - Inclusive ending virtual address of the symbol within the mapping.
    - **Field: Name**: string - Name of the symbol.

### Mapping lifetimes and ordering

A mapping overlays the latest still-live mapping at each address it covers. Unloading the overlay reveals the earlier live mapping; loading an overlay does not implicitly unload what lies beneath it. To replace a mapping permanently, explicitly unload the old mapping before loading its replacement. No filename (including `vmlinux`) has special unload semantics.

An unload applies at its timestamp, so samples exactly at that timestamp see the remaining live mappings, not the unloaded range. At equal timestamps, emit ProcessCreate first, then unloads, then mappings and their symbols, then samples, and finally ProcessExit. TraceLog also accommodates samples written before mapping changes at the same timestamp.

TraceLog persists these lifetimes in ETLX for module lookup, including partial unloads and restored overlays. `GetLoadedModule` returns a mapping while any portion remains live; `GetModuleContainingAddress` returns the mapping visible at the requested address and time.

Readers that do not recognize ProcessMappingUnload (stable event ID 6) can continue to parse the trace but may retain stale symbols after an unload. No lifecycle completeness signal or tombstone compatibility mode is provided.

## Universal Events Provider
The universal events provider is named "Universal.Events". It's purpose is to provide events that contain a value and have a callstack. There are no stable event IDs, but there will be a set of stable names.  Required fields as listed above in this document are used to assist tools in interpreting the events.

### Current Stable Event Names
 - cpu - Represents a CPU sample.
 - cswitch - Represents a machine-level context switch.

### Event Definition
 - **Field: Value**: varint - Value of the event.

### Example: cpu
 - Each cpu event represents a CPU sample.
 - The value represents the weight of the sample.
 - As an example if emitting one CPU sample per core per millisecond, emit an event with Value=1 per core every millisecond.

### Example: cswitch
 - Each cswitch event represents an OS-level context switch.
 - The thread ID provided in the set of required fields is the thread being switched in.
 - The CPU number provided in the set of required fields is the CPU involved.
 - The value represents the amount of time that that has elapsed since the thread last executed (switched out time).  Times are based on the QPC frequency from the source containing the events.
 - The callstack associated with the event is the stack where the thread switched out (blocked).