# EventPipe Event Serialization Format

This document describes the format for serializing event data processed by EventPipe.


## Structure of Events

* Each Event Starts with a header (int is a 4 byte little endian, long is an 8 byte little endian integer).  

```
        int EventSize;    // Size bytes of this header and the payload and stacks.  Does NOT encode the size of the EventSize field itself. 
        int MetaDataId;   // a number identifying the description of this event.  0 is special as described below
        int ThreadId;
        long TimeStamp;
        Guid ActivityID;
        Guid RelatedActivityID;
        int PayloadSize; 
```

* After this header is the Payload itself (of size PayloadSize bytes);   Payloads are always rounded up to a 4 byte quantity. 

* After that is integer (4 bytes) representing the count of bytes needed to represent the stack addresses.  If there is no stack this count is 0.
* After that is the bytes representing the stacks.  These may be 4 bytes or 8 bytes depending on the machine word size. (The pointer size is
one of the data values associated with the header for the entire file).
 
Events follow one another directly, Because Stacks and Payload are always rounded up to 4 byte boundaries, the total size of an event is always a
multiple of 4.  


## Structure of MetaData

As mentioned above every event has a MetaDataID, which is a small integer value.   Associated with each such ID is a blob of serialized meta data 
This meta-data is sent just like any other event, but its MetaData ID is 0.   The PayloadBytes of such a MetaData definition are

```
    int MetaDataId;      // The Meta-Data ID that is being defined.
    string ProviderName; // The 2 byte Unicode, null terminated string representing the Name of the Provider (e.g. EventSource)
    int EventId;         // A small number that uniquely represents this Event within this provider.  
    string EventName;    // The 2 byte Unicode, null terminated string representing the Name of the Event
    long Keywords;       // 64 bit set of groups (keywords) that this event belongs to.
    int Version          // The version number for this event.
    int Level;           // The verbosity (5 is verbose, 1 is only critical) for the event.
```

### Payload Description
Following this header there is a Payload description.   This consists of 

*   int FieldCount;      // The number of fields in the payload

Followed by FieldCount number of field Definitions 
``` 
    int TypeCode;	 // This is the System.Typecode enumeration
    <PAYLOAD_DESCRIPTION>
    string FieldName;    // The 2 byte Unicode, null terminated string representing the Name of the Field
```

For primitive types and strings <PAYLOAD_DESCRIPTION> is not present, however if TypeCode == Object (1) then <PAYLOAD_DESCRIPTION> another payload
description (that is a field count, followed by a list of field definitions).   These can be nested to arbitrary depth.  


## Deserializing Payloads

No attempt is made to be sophisticated about serializing the payload fields.   Each primitive type is serialized in little endian format.  Strings 
are serialized as 2 byte unicode, null terminated strings.   Everything is serialized as its natural size and no alignment is done between fields
(everything is packed without spacing).  However at the end of the payload bytes, alignment is done to insure that the total payload size is a multiple
of 4.   

